// Prompt: inventory grid system
// Type: general

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using System;

public class InventoryGridSystem : MonoBehaviour
{
    [System.Serializable]
    public class InventoryItem
    {
        public string itemName;
        public Sprite itemIcon;
        public int stackSize = 1;
        public int currentStack = 1;
        public ItemType itemType;
        
        public InventoryItem(string name, Sprite icon, ItemType type, int stack = 1)
        {
            itemName = name;
            itemIcon = icon;
            itemType = type;
            stackSize = stack;
            currentStack = 1;
        }
    }
    
    public enum ItemType
    {
        Consumable,
        Equipment,
        Material,
        Quest
    }
    
    [System.Serializable]
    public class GridSlot
    {
        public InventoryItem item;
        public bool isEmpty = true;
        public Vector2Int gridPosition;
        
        public void SetItem(InventoryItem newItem)
        {
            item = newItem;
            isEmpty = false;
        }
        
        public void ClearSlot()
        {
            item = null;
            isEmpty = true;
        }
    }
    
    [Header("Grid Settings")]
    [SerializeField] private int _gridWidth = 8;
    [SerializeField] private int _gridHeight = 6;
    [SerializeField] private float _slotSize = 64f;
    [SerializeField] private float _slotSpacing = 4f;
    
    [Header("UI References")]
    [SerializeField] private Transform _gridParent;
    [SerializeField] private GameObject _slotPrefab;
    [SerializeField] private Canvas _inventoryCanvas;
    
    [Header("Drag and Drop")]
    [SerializeField] private GameObject _dragPreviewPrefab;
    [SerializeField] private LayerMask _slotLayerMask = -1;
    
    private GridSlot[,] _inventoryGrid;
    private GameObject[,] _slotObjects;
    private Camera _uiCamera;
    private InventorySlotUI _draggedSlot;
    private GameObject _dragPreview;
    private bool _isDragging;
    
    public event Action<InventoryItem> OnItemAdded;
    public event Action<InventoryItem> OnItemRemoved;
    public event Action OnInventoryChanged;
    
    private void Start()
    {
        _uiCamera = _inventoryCanvas.worldCamera ?? Camera.main;
        InitializeGrid();
        CreateGridUI();
    }
    
    private void Update()
    {
        HandleDragAndDrop();
    }
    
    private void InitializeGrid()
    {
        _inventoryGrid = new GridSlot[_gridWidth, _gridHeight];
        _slotObjects = new GameObject[_gridWidth, _gridHeight];
        
        for (int x = 0; x < _gridWidth; x++)
        {
            for (int y = 0; y < _gridHeight; y++)
            {
                _inventoryGrid[x, y] = new GridSlot();
                _inventoryGrid[x, y].gridPosition = new Vector2Int(x, y);
            }
        }
    }
    
    private void CreateGridUI()
    {
        if (_gridParent == null || _slotPrefab == null) return;
        
        for (int x = 0; x < _gridWidth; x++)
        {
            for (int y = 0; y < _gridHeight; y++)
            {
                GameObject slotObj = Instantiate(_slotPrefab, _gridParent);
                RectTransform slotRect = slotObj.GetComponent<RectTransform>();
                
                float posX = x * (_slotSize + _slotSpacing);
                float posY = -y * (_slotSize + _slotSpacing);
                slotRect.anchoredPosition = new Vector2(posX, posY);
                slotRect.sizeDelta = new Vector2(_slotSize, _slotSize);
                
                InventorySlotUI slotUI = slotObj.GetComponent<InventorySlotUI>();
                if (slotUI == null)
                    slotUI = slotObj.AddComponent<InventorySlotUI>();
                
                slotUI.Initialize(new Vector2Int(x, y), this);
                _slotObjects[x, y] = slotObj;
            }
        }
    }
    
    public bool AddItem(InventoryItem item)
    {
        if (item == null) return false;
        
        // Try to stack with existing items first
        if (TryStackItem(item)) return true;
        
        // Find empty slot
        Vector2Int emptySlot = FindEmptySlot();
        if (emptySlot.x == -1) return false;
        
        _inventoryGrid[emptySlot.x, emptySlot.y].SetItem(item);
        UpdateSlotUI(emptySlot.x, emptySlot.y);
        
        OnItemAdded?.Invoke(item);
        OnInventoryChanged?.Invoke();
        return true;
    }
    
    public bool RemoveItem(Vector2Int gridPos)
    {
        if (!IsValidPosition(gridPos) || _inventoryGrid[gridPos.x, gridPos.y].isEmpty)
            return false;
        
        InventoryItem removedItem = _inventoryGrid[gridPos.x, gridPos.y].item;
        _inventoryGrid[gridPos.x, gridPos.y].ClearSlot();
        UpdateSlotUI(gridPos.x, gridPos.y);
        
        OnItemRemoved?.Invoke(removedItem);
        OnInventoryChanged?.Invoke();
        return true;
    }
    
    public bool MoveItem(Vector2Int fromPos, Vector2Int toPos)
    {
        if (!IsValidPosition(fromPos) || !IsValidPosition(toPos))
            return false;
        
        if (_inventoryGrid[fromPos.x, fromPos.y].isEmpty)
            return false;
        
        // Swap items
        GridSlot fromSlot = _inventoryGrid[fromPos.x, fromPos.y];
        GridSlot toSlot = _inventoryGrid[toPos.x, toPos.y];
        
        _inventoryGrid[toPos.x, toPos.y] = fromSlot;
        _inventoryGrid[fromPos.x, fromPos.y] = toSlot;
        
        _inventoryGrid[toPos.x, toPos.y].gridPosition = toPos;
        _inventoryGrid[fromPos.x, fromPos.y].gridPosition = fromPos;
        
        UpdateSlotUI(fromPos.x, fromPos.y);
        UpdateSlotUI(toPos.x, toPos.y);
        
        OnInventoryChanged?.Invoke();
        return true;
    }
    
    private bool TryStackItem(InventoryItem item)
    {
        for (int x = 0; x < _gridWidth; x++)
        {
            for (int y = 0; y < _gridHeight; y++)
            {
                GridSlot slot = _inventoryGrid[x, y];
                if (!slot.isEmpty && slot.item.itemName == item.itemName)
                {
                    if (slot.item.currentStack < slot.item.stackSize)
                    {
                        int spaceLeft = slot.item.stackSize - slot.item.currentStack;
                        int toAdd = Mathf.Min(spaceLeft, item.currentStack);
                        slot.item.currentStack += toAdd;
                        item.currentStack -= toAdd;
                        
                        UpdateSlotUI(x, y);
                        
                        if (item.currentStack <= 0)
                            return true;
                    }
                }
            }
        }
        return false;
    }
    
    private Vector2Int FindEmptySlot()
    {
        for (int y = 0; y < _gridHeight; y++)
        {
            for (int x = 0; x < _gridWidth; x++)
            {
                if (_inventoryGrid[x, y].isEmpty)
                    return new Vector2Int(x, y);
            }
        }
        return new Vector2Int(-1, -1);
    }
    
    private bool IsValidPosition(Vector2Int pos)
    {
        return pos.x >= 0 && pos.x < _gridWidth && pos.y >= 0 && pos.y < _gridHeight;
    }
    
    private void UpdateSlotUI(int x, int y)
    {
        if (_slotObjects[x, y] != null)
        {
            InventorySlotUI slotUI = _slotObjects[x, y].GetComponent<InventorySlotUI>();
            if (slotUI != null)
            {
                slotUI.UpdateSlot(_inventoryGrid[x, y]);
            }
        }
    }
    
    private void HandleDragAndDrop()
    {
        if (_isDragging && _dragPreview != null)
        {
            Vector3 mousePos = Input.mousePosition;
            _dragPreview.transform.position = mousePos;
        }
    }
    
    public void StartDrag(InventorySlotUI slotUI)
    {
        if (slotUI.GridSlot.isEmpty) return;
        
        _draggedSlot = slotUI;
        _isDragging = true;
        
        if (_dragPreviewPrefab != null)
        {
            _dragPreview = Instantiate(_dragPreviewPrefab, _inventoryCanvas.transform);
            Image previewImage = _dragPreview.GetComponent<Image>();
            if (previewImage != null)
            {
                previewImage.sprite = slotUI.GridSlot.item.itemIcon;
            }
        }
    }
    
    public void EndDrag(InventorySlotUI targetSlot)
    {
        if (!_isDragging || _draggedSlot == null) return;
        
        if (targetSlot != null && targetSlot != _draggedSlot)
        {
            MoveItem(_draggedSlot.GridPosition, targetSlot.GridPosition);
        }
        
        _isDragging = false;
        _draggedSlot = null;
        
        if (_dragPreview != null)
        {
            Destroy(_dragPreview);
            _dragPreview = null;
        }
    }
    
    public GridSlot GetSlot(Vector2Int position)
    {
        if (!IsValidPosition(position)) return null;
        return _inventoryGrid[position.x, position.y];
    }
    
    public List<InventoryItem> GetAllItems()
    {
        List<InventoryItem> items = new List<InventoryItem>();
        
        for (int x = 0; x < _gridWidth; x++)
        {
            for (int y = 0; y < _gridHeight; y++)
            {
                if (!_inventoryGrid[x, y].isEmpty)
                {
                    items.Add(_inventoryGrid[x, y].item);
                }
            }
        }
        
        return items;
    }
    
    public void ClearInventory()
    {
        for (int x = 0; x < _gridWidth; x++)
        {
            for (int y = 0; y < _gridHeight; y++)
            {
                _inventoryGrid[x, y].ClearSlot();
                UpdateSlotUI(x, y);
            }
        }
        
        OnInventoryChanged?.Invoke();
    }
}

public class InventorySlotUI : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI Components")]
    [SerializeField] private Image _itemIcon;
    [SerializeField] private Text _stackText;
    [SerializeField] private Image _background;
    [SerializeField] private Color _normalColor = Color.white;
    [SerializeField] private Color _highlightColor = Color.yellow;
    
    private Vector2Int _gridPosition;
    private InventoryGridSystem _inventorySystem;
    private InventoryGridSystem.GridSlot _gridSlot;
    
    public Vector2Int GridPosition => _gridPosition;
    public InventoryGridSystem.GridSlot GridSlot => _gridSlot;
    
    public void Initialize(Vector2Int position, InventoryGridSystem inventorySystem)
    {
        _gridPosition = position;
        _inventorySystem = inventorySystem;
        
        if (_itemIcon == null) _itemIcon = GetComponentInChildren<Image>();
        if (_stackText == null) _stackText = GetComponentInChildren<Text>();
        if (_background == null) _background = GetComponent<Image>();
        
        UpdateSlot(_inventorySystem.GetSlot(_gridPosition));
    }
    
    public void UpdateSlot(InventoryGridSystem.GridSlot slot)
    {
        _gridSlot = slot;
        
        if (slot == null || slot.isEmpty)
        {
            if (_itemIcon != null) _itemIcon.sprite = null;
            if (_itemIcon != null) _itemIcon.color = Color.clear;
            if (_stackText != null) _stackText.text = "";
        }
        else
        {
            if (_itemIcon != null)
            {
                _itemIcon.sprite = slot.item.itemIcon;
                _itemIcon.color = Color.white;
            }
            
            if (_stackText != null)
            {
                _stackText.text = slot.item.currentStack > 1 ? slot.item.currentStack.ToString() : "";
            }
        }
    }
    
    public void OnPointerDown(PointerEventData eventData)
    {
        if (_inventorySystem != null && !_gridSlot.isEmpty)
        {
            _inventorySystem.StartDrag(this);
        }
    }
    
    public void OnPointerUp(PointerEventData eventData)
    {
        if (_inventorySystem != null)
        {
            _inventorySystem.EndDrag(this);
        }
    }
    
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_background != null)
        {
            _background.color = _highlightColor;
        }
    }
    
    public void OnPointerExit(PointerEventData eventData)
    {
        if (_background != null)
        {
            _background.color = _normalColor;
        }
    }
}