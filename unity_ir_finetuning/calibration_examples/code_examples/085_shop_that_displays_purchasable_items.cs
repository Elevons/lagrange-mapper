// Prompt: shop that displays purchasable items
// Type: general

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections.Generic;
using System;

public class Shop : MonoBehaviour
{
    [System.Serializable]
    public class ShopItem
    {
        [Header("Item Info")]
        public string itemName;
        public string description;
        public Sprite icon;
        public int price;
        public int maxQuantity = 1;
        
        [Header("Item Data")]
        public GameObject itemPrefab;
        public bool isConsumable = false;
        
        [HideInInspector]
        public int currentQuantity = 0;
    }

    [System.Serializable]
    public class ShopItemUI
    {
        public GameObject itemPanel;
        public Image itemIcon;
        public Text itemName;
        public Text itemDescription;
        public Text itemPrice;
        public Text quantityText;
        public Button purchaseButton;
        public Button sellButton;
        
        [HideInInspector]
        public ShopItem linkedItem;
    }

    [Header("Shop Configuration")]
    [SerializeField] private List<ShopItem> _shopItems = new List<ShopItem>();
    [SerializeField] private GameObject _shopPanel;
    [SerializeField] private GameObject _itemUIPrefab;
    [SerializeField] private Transform _itemContainer;
    [SerializeField] private ScrollRect _scrollRect;
    
    [Header("Player Currency")]
    [SerializeField] private int _playerMoney = 1000;
    [SerializeField] private Text _moneyDisplay;
    
    [Header("UI Elements")]
    [SerializeField] private Button _closeShopButton;
    [SerializeField] private Text _shopTitle;
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _purchaseSound;
    [SerializeField] private AudioClip _sellSound;
    [SerializeField] private AudioClip _errorSound;
    
    [Header("Events")]
    public UnityEvent<string, int> OnItemPurchased;
    public UnityEvent<string, int> OnItemSold;
    public UnityEvent<int> OnMoneyChanged;
    
    private List<ShopItemUI> _itemUIElements = new List<ShopItemUI>();
    private bool _isShopOpen = false;
    private Dictionary<string, int> _playerInventory = new Dictionary<string, int>();

    private void Start()
    {
        InitializeShop();
        UpdateMoneyDisplay();
        
        if (_closeShopButton != null)
            _closeShopButton.onClick.AddListener(CloseShop);
            
        CloseShop();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) && _isShopOpen)
        {
            CloseShop();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            OpenShop();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            CloseShop();
        }
    }

    private void InitializeShop()
    {
        if (_itemContainer == null || _itemUIPrefab == null) return;

        foreach (Transform child in _itemContainer)
        {
            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);
        }

        _itemUIElements.Clear();

        for (int i = 0; i < _shopItems.Count; i++)
        {
            CreateItemUI(_shopItems[i]);
        }
    }

    private void CreateItemUI(ShopItem item)
    {
        GameObject itemUI = Instantiate(_itemUIPrefab, _itemContainer);
        ShopItemUI uiElement = new ShopItemUI();

        uiElement.itemPanel = itemUI;
        uiElement.itemIcon = itemUI.transform.Find("Icon")?.GetComponent<Image>();
        uiElement.itemName = itemUI.transform.Find("ItemName")?.GetComponent<Text>();
        uiElement.itemDescription = itemUI.transform.Find("Description")?.GetComponent<Text>();
        uiElement.itemPrice = itemUI.transform.Find("Price")?.GetComponent<Text>();
        uiElement.quantityText = itemUI.transform.Find("Quantity")?.GetComponent<Text>();
        uiElement.purchaseButton = itemUI.transform.Find("PurchaseButton")?.GetComponent<Button>();
        uiElement.sellButton = itemUI.transform.Find("SellButton")?.GetComponent<Button>();
        uiElement.linkedItem = item;

        if (uiElement.itemIcon != null && item.icon != null)
            uiElement.itemIcon.sprite = item.icon;

        if (uiElement.itemName != null)
            uiElement.itemName.text = item.itemName;

        if (uiElement.itemDescription != null)
            uiElement.itemDescription.text = item.description;

        if (uiElement.itemPrice != null)
            uiElement.itemPrice.text = "$" + item.price.ToString();

        if (uiElement.purchaseButton != null)
        {
            uiElement.purchaseButton.onClick.AddListener(() => PurchaseItem(item));
        }

        if (uiElement.sellButton != null)
        {
            uiElement.sellButton.onClick.AddListener(() => SellItem(item));
        }

        _itemUIElements.Add(uiElement);
        UpdateItemUI(uiElement);
    }

    private void UpdateItemUI(ShopItemUI uiElement)
    {
        if (uiElement.linkedItem == null) return;

        int ownedQuantity = GetPlayerItemQuantity(uiElement.linkedItem.itemName);
        
        if (uiElement.quantityText != null)
        {
            uiElement.quantityText.text = "Owned: " + ownedQuantity.ToString();
        }

        bool canPurchase = _playerMoney >= uiElement.linkedItem.price && 
                          ownedQuantity < uiElement.linkedItem.maxQuantity;
        
        if (uiElement.purchaseButton != null)
        {
            uiElement.purchaseButton.interactable = canPurchase;
        }

        bool canSell = ownedQuantity > 0;
        if (uiElement.sellButton != null)
        {
            uiElement.sellButton.interactable = canSell;
            uiElement.sellButton.gameObject.SetActive(canSell);
        }
    }

    private void UpdateAllItemUI()
    {
        foreach (var uiElement in _itemUIElements)
        {
            UpdateItemUI(uiElement);
        }
    }

    public void OpenShop()
    {
        if (_shopPanel != null)
        {
            _shopPanel.SetActive(true);
            _isShopOpen = true;
            UpdateAllItemUI();
            Time.timeScale = 0f;
        }
    }

    public void CloseShop()
    {
        if (_shopPanel != null)
        {
            _shopPanel.SetActive(false);
            _isShopOpen = false;
            Time.timeScale = 1f;
        }
    }

    public void PurchaseItem(ShopItem item)
    {
        if (item == null) return;

        int ownedQuantity = GetPlayerItemQuantity(item.itemName);
        
        if (_playerMoney >= item.price && ownedQuantity < item.maxQuantity)
        {
            _playerMoney -= item.price;
            AddItemToInventory(item.itemName, 1);
            
            UpdateMoneyDisplay();
            UpdateAllItemUI();
            
            PlaySound(_purchaseSound);
            OnItemPurchased?.Invoke(item.itemName, 1);
            OnMoneyChanged?.Invoke(_playerMoney);
            
            Debug.Log($"Purchased {item.itemName} for ${item.price}");
        }
        else
        {
            PlaySound(_errorSound);
            string reason = _playerMoney < item.price ? "Not enough money!" : "Maximum quantity reached!";
            Debug.Log($"Cannot purchase {item.itemName}: {reason}");
        }
    }

    public void SellItem(ShopItem item)
    {
        if (item == null) return;

        int ownedQuantity = GetPlayerItemQuantity(item.itemName);
        
        if (ownedQuantity > 0)
        {
            int sellPrice = Mathf.RoundToInt(item.price * 0.5f);
            _playerMoney += sellPrice;
            RemoveItemFromInventory(item.itemName, 1);
            
            UpdateMoneyDisplay();
            UpdateAllItemUI();
            
            PlaySound(_sellSound);
            OnItemSold?.Invoke(item.itemName, 1);
            OnMoneyChanged?.Invoke(_playerMoney);
            
            Debug.Log($"Sold {item.itemName} for ${sellPrice}");
        }
        else
        {
            PlaySound(_errorSound);
            Debug.Log($"Cannot sell {item.itemName}: You don't own any!");
        }
    }

    private void AddItemToInventory(string itemName, int quantity)
    {
        if (_playerInventory.ContainsKey(itemName))
        {
            _playerInventory[itemName] += quantity;
        }
        else
        {
            _playerInventory[itemName] = quantity;
        }
    }

    private void RemoveItemFromInventory(string itemName, int quantity)
    {
        if (_playerInventory.ContainsKey(itemName))
        {
            _playerInventory[itemName] = Mathf.Max(0, _playerInventory[itemName] - quantity);
            
            if (_playerInventory[itemName] == 0)
            {
                _playerInventory.Remove(itemName);
            }
        }
    }

    private int GetPlayerItemQuantity(string itemName)
    {
        return _playerInventory.ContainsKey(itemName) ? _playerInventory[itemName] : 0;
    }

    private void UpdateMoneyDisplay()
    {
        if (_moneyDisplay != null)
        {
            _moneyDisplay.text = "$" + _playerMoney.ToString();
        }
    }

    private void PlaySound(AudioClip clip)
    {
        if (_audioSource != null && clip != null)
        {
            _audioSource.PlayOneShot(clip);
        }
    }

    public void AddMoney(int amount)
    {
        _playerMoney += amount;
        UpdateMoneyDisplay();
        OnMoneyChanged?.Invoke(_playerMoney);
    }

    public bool HasMoney(int amount)
    {
        return _playerMoney >= amount;
    }

    public int GetMoney()
    {
        return _playerMoney;
    }

    public bool HasItem(string itemName, int quantity = 1)
    {
        return GetPlayerItemQuantity(itemName) >= quantity;
    }

    public Dictionary<string, int> GetInventory()
    {
        return new Dictionary<string, int>(_playerInventory);
    }
}