// Prompt: vending machine with selection
// Type: general

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections.Generic;
using System.Collections;

public class VendingMachine : MonoBehaviour
{
    [System.Serializable]
    public class VendingItem
    {
        public string itemName;
        public Sprite itemIcon;
        public GameObject itemPrefab;
        public int price;
        public int stock;
        public string description;
    }

    [System.Serializable]
    public class PurchaseEvent : UnityEvent<string, int> { }

    [Header("Vending Machine Settings")]
    [SerializeField] private List<VendingItem> _availableItems = new List<VendingItem>();
    [SerializeField] private Transform _dispensingPoint;
    [SerializeField] private float _interactionRange = 3f;
    [SerializeField] private LayerMask _playerLayer = -1;

    [Header("UI References")]
    [SerializeField] private Canvas _vendingUI;
    [SerializeField] private Transform _itemButtonContainer;
    [SerializeField] private Button _itemButtonPrefab;
    [SerializeField] private Text _moneyDisplay;
    [SerializeField] private Text _messageDisplay;
    [SerializeField] private Button _closeButton;

    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _purchaseSound;
    [SerializeField] private AudioClip _errorSound;
    [SerializeField] private AudioClip _dispensingSound;

    [Header("Animation")]
    [SerializeField] private Animator _machineAnimator;
    [SerializeField] private ParticleSystem _dispensingEffect;

    [Header("Events")]
    public PurchaseEvent OnItemPurchased;

    private bool _isPlayerNearby = false;
    private GameObject _currentPlayer;
    private int _playerMoney = 100;
    private List<Button> _itemButtons = new List<Button>();
    private VendingItem _selectedItem;
    private bool _isDispensing = false;

    private void Start()
    {
        InitializeUI();
        SetupEventListeners();
        UpdateMoneyDisplay();
        
        if (_vendingUI != null)
            _vendingUI.gameObject.SetActive(false);
    }

    private void Update()
    {
        CheckForPlayer();
        HandleInput();
    }

    private void InitializeUI()
    {
        if (_itemButtonContainer == null || _itemButtonPrefab == null) return;

        foreach (Transform child in _itemButtonContainer)
        {
            if (child != _itemButtonPrefab.transform)
                DestroyImmediate(child.gameObject);
        }

        _itemButtons.Clear();

        for (int i = 0; i < _availableItems.Count; i++)
        {
            VendingItem item = _availableItems[i];
            Button itemButton = Instantiate(_itemButtonPrefab, _itemButtonContainer);
            itemButton.gameObject.SetActive(true);

            Text[] texts = itemButton.GetComponentsInChildren<Text>();
            Image[] images = itemButton.GetComponentsInChildren<Image>();

            if (texts.Length > 0) texts[0].text = item.itemName;
            if (texts.Length > 1) texts[1].text = $"${item.price}";
            if (texts.Length > 2) texts[2].text = $"Stock: {item.stock}";

            if (images.Length > 1 && item.itemIcon != null)
                images[1].sprite = item.itemIcon;

            int index = i;
            itemButton.onClick.AddListener(() => SelectItem(index));
            
            _itemButtons.Add(itemButton);
        }

        if (_itemButtonPrefab != null)
            _itemButtonPrefab.gameObject.SetActive(false);
    }

    private void SetupEventListeners()
    {
        if (_closeButton != null)
            _closeButton.onClick.AddListener(CloseVendingMachine);
    }

    private void CheckForPlayer()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, _interactionRange, _playerLayer);
        bool playerFound = false;

        foreach (Collider col in colliders)
        {
            if (col.CompareTag("Player"))
            {
                if (!_isPlayerNearby)
                {
                    _isPlayerNearby = true;
                    _currentPlayer = col.gameObject;
                    ShowInteractionPrompt();
                }
                playerFound = true;
                break;
            }
        }

        if (!playerFound && _isPlayerNearby)
        {
            _isPlayerNearby = false;
            _currentPlayer = null;
            HideInteractionPrompt();
            CloseVendingMachine();
        }
    }

    private void HandleInput()
    {
        if (_isPlayerNearby && Input.GetKeyDown(KeyCode.E))
        {
            if (_vendingUI != null && !_vendingUI.gameObject.activeInHierarchy)
            {
                OpenVendingMachine();
            }
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CloseVendingMachine();
        }
    }

    private void OpenVendingMachine()
    {
        if (_vendingUI != null)
        {
            _vendingUI.gameObject.SetActive(true);
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            
            RefreshItemButtons();
            ShowMessage("Select an item to purchase");
        }
    }

    private void CloseVendingMachine()
    {
        if (_vendingUI != null && _vendingUI.gameObject.activeInHierarchy)
        {
            _vendingUI.gameObject.SetActive(false);
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            _selectedItem = null;
        }
    }

    private void SelectItem(int itemIndex)
    {
        if (itemIndex < 0 || itemIndex >= _availableItems.Count) return;

        _selectedItem = _availableItems[itemIndex];
        
        if (_selectedItem.stock <= 0)
        {
            ShowMessage("Item out of stock!");
            PlayErrorSound();
            return;
        }

        if (_playerMoney < _selectedItem.price)
        {
            ShowMessage("Insufficient funds!");
            PlayErrorSound();
            return;
        }

        PurchaseItem(_selectedItem, itemIndex);
    }

    private void PurchaseItem(VendingItem item, int itemIndex)
    {
        if (_isDispensing) return;

        _playerMoney -= item.price;
        item.stock--;
        
        UpdateMoneyDisplay();
        RefreshItemButtons();
        
        ShowMessage($"Purchased {item.itemName}!");
        PlayPurchaseSound();
        
        StartCoroutine(DispenseItem(item));
        
        OnItemPurchased?.Invoke(item.itemName, item.price);
    }

    private IEnumerator DispenseItem(VendingItem item)
    {
        _isDispensing = true;

        if (_machineAnimator != null)
            _machineAnimator.SetTrigger("Dispense");

        yield return new WaitForSeconds(0.5f);

        if (_dispensingEffect != null)
            _dispensingEffect.Play();

        if (_dispensingSound != null && _audioSource != null)
            _audioSource.PlayOneShot(_dispensingSound);

        yield return new WaitForSeconds(1f);

        if (item.itemPrefab != null && _dispensingPoint != null)
        {
            GameObject dispensedItem = Instantiate(item.itemPrefab, _dispensingPoint.position, _dispensingPoint.rotation);
            
            Rigidbody rb = dispensedItem.GetComponent<Rigidbody>();
            if (rb == null)
                rb = dispensedItem.AddComponent<Rigidbody>();
            
            rb.AddForce(Vector3.forward * 2f + Vector3.down * 1f, ForceMode.Impulse);
        }

        _isDispensing = false;
    }

    private void RefreshItemButtons()
    {
        for (int i = 0; i < _itemButtons.Count && i < _availableItems.Count; i++)
        {
            VendingItem item = _availableItems[i];
            Button button = _itemButtons[i];
            
            Text[] texts = button.GetComponentsInChildren<Text>();
            if (texts.Length > 2)
                texts[2].text = $"Stock: {item.stock}";

            button.interactable = item.stock > 0 && _playerMoney >= item.price;
            
            if (item.stock <= 0)
            {
                button.GetComponent<Image>().color = Color.gray;
            }
            else if (_playerMoney < item.price)
            {
                button.GetComponent<Image>().color = Color.red;
            }
            else
            {
                button.GetComponent<Image>().color = Color.white;
            }
        }
    }

    private void UpdateMoneyDisplay()
    {
        if (_moneyDisplay != null)
            _moneyDisplay.text = $"Money: ${_playerMoney}";
    }

    private void ShowMessage(string message)
    {
        if (_messageDisplay != null)
        {
            _messageDisplay.text = message;
            StopCoroutine(nameof(ClearMessageAfterDelay));
            StartCoroutine(ClearMessageAfterDelay());
        }
    }

    private IEnumerator ClearMessageAfterDelay()
    {
        yield return new WaitForSecondsRealtime(3f);
        if (_messageDisplay != null)
            _messageDisplay.text = "";
    }

    private void ShowInteractionPrompt()
    {
        // Override this method to show interaction UI
    }

    private void HideInteractionPrompt()
    {
        // Override this method to hide interaction UI
    }

    private void PlayPurchaseSound()
    {
        if (_audioSource != null && _purchaseSound != null)
            _audioSource.PlayOneShot(_purchaseSound);
    }

    private void PlayErrorSound()
    {
        if (_audioSource != null && _errorSound != null)
            _audioSource.PlayOneShot(_errorSound);
    }

    public void AddMoney(int amount)
    {
        _playerMoney += amount;
        UpdateMoneyDisplay();
        RefreshItemButtons();
    }

    public void RestockItem(int itemIndex, int amount)
    {
        if (itemIndex >= 0 && itemIndex < _availableItems.Count)
        {
            _availableItems[itemIndex].stock += amount;
            RefreshItemButtons();
        }
    }

    public void RestockAllItems(int amount)
    {
        for (int i = 0; i < _availableItems.Count; i++)
        {
            _availableItems[i].stock += amount;
        }
        RefreshItemButtons();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _interactionRange);
        
        if (_dispensingPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(_dispensingPoint.position, Vector3.one * 0.2f);
        }
    }
}