// Prompt: color matching puzzle blocks
// Type: general

using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using System.Collections;

public class ColorMatchingPuzzle : MonoBehaviour
{
    [System.Serializable]
    public class PuzzleBlock
    {
        public GameObject blockObject;
        public Renderer blockRenderer;
        public Color currentColor;
        public Vector2Int gridPosition;
        public bool isMatched;
        
        public PuzzleBlock(GameObject obj, Vector2Int pos)
        {
            blockObject = obj;
            blockRenderer = obj.GetComponent<Renderer>();
            gridPosition = pos;
            isMatched = false;
        }
    }
    
    [System.Serializable]
    public class ColorSet
    {
        public Color color;
        public Material material;
    }
    
    [Header("Grid Settings")]
    [SerializeField] private int _gridWidth = 5;
    [SerializeField] private int _gridHeight = 5;
    [SerializeField] private float _blockSpacing = 1.1f;
    [SerializeField] private GameObject _blockPrefab;
    
    [Header("Colors")]
    [SerializeField] private ColorSet[] _availableColors;
    [SerializeField] private int _minMatchCount = 3;
    
    [Header("Gameplay")]
    [SerializeField] private float _swapAnimationDuration = 0.3f;
    [SerializeField] private float _matchDelay = 0.5f;
    [SerializeField] private float _fallSpeed = 5f;
    
    [Header("Effects")]
    [SerializeField] private ParticleSystem _matchEffectPrefab;
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _swapSound;
    [SerializeField] private AudioClip _matchSound;
    
    [Header("Events")]
    public UnityEvent<int> OnBlocksMatched;
    public UnityEvent OnPuzzleCompleted;
    public UnityEvent OnNoMovesAvailable;
    
    private PuzzleBlock[,] _grid;
    private PuzzleBlock _selectedBlock;
    private bool _isProcessing;
    private Camera _mainCamera;
    private int _totalMatches;
    
    private void Start()
    {
        _mainCamera = Camera.main;
        if (_mainCamera == null)
            _mainCamera = FindObjectOfType<Camera>();
            
        InitializeGrid();
        GenerateInitialBlocks();
    }
    
    private void Update()
    {
        if (!_isProcessing)
        {
            HandleInput();
        }
    }
    
    private void InitializeGrid()
    {
        _grid = new PuzzleBlock[_gridWidth, _gridHeight];
        
        for (int x = 0; x < _gridWidth; x++)
        {
            for (int y = 0; y < _gridHeight; y++)
            {
                Vector3 position = new Vector3(
                    (x - _gridWidth / 2f) * _blockSpacing,
                    (y - _gridHeight / 2f) * _blockSpacing,
                    0
                );
                
                GameObject blockObj = Instantiate(_blockPrefab, position, Quaternion.identity, transform);
                blockObj.name = $"Block_{x}_{y}";
                
                PuzzleBlock block = new PuzzleBlock(blockObj, new Vector2Int(x, y));
                _grid[x, y] = block;
                
                // Add collider for mouse interaction
                if (blockObj.GetComponent<Collider>() == null)
                {
                    blockObj.AddComponent<BoxCollider>();
                }
            }
        }
    }
    
    private void GenerateInitialBlocks()
    {
        for (int x = 0; x < _gridWidth; x++)
        {
            for (int y = 0; y < _gridHeight; y++)
            {
                AssignRandomColor(_grid[x, y]);
            }
        }
        
        // Ensure no initial matches
        while (HasMatches())
        {
            for (int x = 0; x < _gridWidth; x++)
            {
                for (int y = 0; y < _gridHeight; y++)
                {
                    if (IsPartOfMatch(_grid[x, y]))
                    {
                        AssignRandomColor(_grid[x, y]);
                    }
                }
            }
        }
    }
    
    private void AssignRandomColor(PuzzleBlock block)
    {
        if (_availableColors.Length == 0) return;
        
        ColorSet colorSet = _availableColors[Random.Range(0, _availableColors.Length)];
        block.currentColor = colorSet.color;
        
        if (block.blockRenderer != null && colorSet.material != null)
        {
            block.blockRenderer.material = colorSet.material;
        }
        else if (block.blockRenderer != null)
        {
            block.blockRenderer.material.color = colorSet.color;
        }
    }
    
    private void HandleInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = _mainCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            
            if (Physics.Raycast(ray, out hit))
            {
                PuzzleBlock clickedBlock = GetBlockFromGameObject(hit.collider.gameObject);
                
                if (clickedBlock != null)
                {
                    if (_selectedBlock == null)
                    {
                        SelectBlock(clickedBlock);
                    }
                    else if (_selectedBlock == clickedBlock)
                    {
                        DeselectBlock();
                    }
                    else if (AreAdjacent(_selectedBlock, clickedBlock))
                    {
                        StartCoroutine(SwapBlocks(_selectedBlock, clickedBlock));
                    }
                    else
                    {
                        SelectBlock(clickedBlock);
                    }
                }
            }
        }
    }
    
    private PuzzleBlock GetBlockFromGameObject(GameObject obj)
    {
        for (int x = 0; x < _gridWidth; x++)
        {
            for (int y = 0; y < _gridHeight; y++)
            {
                if (_grid[x, y].blockObject == obj)
                {
                    return _grid[x, y];
                }
            }
        }
        return null;
    }
    
    private void SelectBlock(PuzzleBlock block)
    {
        DeselectBlock();
        _selectedBlock = block;
        
        // Visual feedback for selection
        if (block.blockRenderer != null)
        {
            block.blockRenderer.material.color = Color.Lerp(block.currentColor, Color.white, 0.3f);
        }
    }
    
    private void DeselectBlock()
    {
        if (_selectedBlock != null && _selectedBlock.blockRenderer != null)
        {
            _selectedBlock.blockRenderer.material.color = _selectedBlock.currentColor;
        }
        _selectedBlock = null;
    }
    
    private bool AreAdjacent(PuzzleBlock block1, PuzzleBlock block2)
    {
        Vector2Int pos1 = block1.gridPosition;
        Vector2Int pos2 = block2.gridPosition;
        
        int deltaX = Mathf.Abs(pos1.x - pos2.x);
        int deltaY = Mathf.Abs(pos1.y - pos2.y);
        
        return (deltaX == 1 && deltaY == 0) || (deltaX == 0 && deltaY == 1);
    }
    
    private IEnumerator SwapBlocks(PuzzleBlock block1, PuzzleBlock block2)
    {
        _isProcessing = true;
        DeselectBlock();
        
        // Play swap sound
        if (_audioSource != null && _swapSound != null)
        {
            _audioSource.PlayOneShot(_swapSound);
        }
        
        // Animate swap
        Vector3 pos1 = block1.blockObject.transform.position;
        Vector3 pos2 = block2.blockObject.transform.position;
        
        float elapsed = 0f;
        while (elapsed < _swapAnimationDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / _swapAnimationDuration;
            
            block1.blockObject.transform.position = Vector3.Lerp(pos1, pos2, t);
            block2.blockObject.transform.position = Vector3.Lerp(pos2, pos1, t);
            
            yield return null;
        }
        
        // Complete swap
        block1.blockObject.transform.position = pos2;
        block2.blockObject.transform.position = pos1;
        
        // Swap colors
        Color tempColor = block1.currentColor;
        Material tempMaterial = block1.blockRenderer.material;
        
        block1.currentColor = block2.currentColor;
        block1.blockRenderer.material = block2.blockRenderer.material;
        
        block2.currentColor = tempColor;
        block2.blockRenderer.material = tempMaterial;
        
        // Check for matches
        yield return new WaitForSeconds(_matchDelay);
        
        if (HasMatches())
        {
            yield return StartCoroutine(ProcessMatches());
        }
        else
        {
            // Swap back if no matches
            yield return StartCoroutine(SwapBlocksBack(block1, block2));
        }
        
        _isProcessing = false;
    }
    
    private IEnumerator SwapBlocksBack(PuzzleBlock block1, PuzzleBlock block2)
    {
        Vector3 pos1 = block1.blockObject.transform.position;
        Vector3 pos2 = block2.blockObject.transform.position;
        
        float elapsed = 0f;
        while (elapsed < _swapAnimationDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / _swapAnimationDuration;
            
            block1.blockObject.transform.position = Vector3.Lerp(pos1, pos2, t);
            block2.blockObject.transform.position = Vector3.Lerp(pos2, pos1, t);
            
            yield return null;
        }
        
        // Swap colors back
        Color tempColor = block1.currentColor;
        Material tempMaterial = block1.blockRenderer.material;
        
        block1.currentColor = block2.currentColor;
        block1.blockRenderer.material = block2.blockRenderer.material;
        
        block2.currentColor = tempColor;
        block2.blockRenderer.material = tempMaterial;
    }
    
    private bool HasMatches()
    {
        for (int x = 0; x < _gridWidth; x++)
        {
            for (int y = 0; y < _gridHeight; y++)
            {
                if (IsPartOfMatch(_grid[x, y]))
                {
                    return true;
                }
            }
        }
        return false;
    }
    
    private bool IsPartOfMatch(PuzzleBlock block)
    {
        return GetHorizontalMatchLength(block) >= _minMatchCount || 
               GetVerticalMatchLength(block) >= _minMatchCount;
    }
    
    private int GetHorizontalMatchLength(PuzzleBlock block)
    {
        int count = 1;
        Color color = block.currentColor;
        Vector2Int pos = block.gridPosition;
        
        // Check left
        for (int x = pos.x - 1; x >= 0; x--)
        {
            if (_grid[x, pos.y].currentColor == color)
                count++;
            else
                break;
        }
        
        // Check right
        for (int x = pos.x + 1; x < _gridWidth; x++)
        {
            if (_grid[x, pos.y].currentColor == color)
                count++;
            else
                break;
        }
        
        return count;
    }
    
    private int GetVerticalMatchLength(PuzzleBlock block)
    {
        int count = 1;
        Color color = block.currentColor;
        Vector2Int pos = block.gridPosition;
        
        // Check down
        for (int y = pos.y - 1; y >= 0; y--)
        {
            if (_grid[pos.x, y].currentColor == color)
                count++;
            else
                break;
        }
        
        // Check up
        for (int y = pos.y + 1; y < _gridHeight; y++)
        {
            if (_grid[pos.x, y].currentColor == color)
                count++;
            else
                break;
        }
        
        return count;
    }
    
    private IEnumerator ProcessMatches()
    {
        List<PuzzleBlock> matchedBlocks = new List<PuzzleBlock>();
        
        // Find all matched blocks
        for (int x = 0; x < _gridWidth; x++)
        {
            for (int y = 0; y < _gridHeight; y++)
            {
                if (IsPartOfMatch(_grid[x, y]))
                {
                    matchedBlocks.Add(_grid[x, y]);
                    _grid[x, y].isMatched = true;
                }
            }
        }
        
        // Play match sound and effects
        if (_audioSource != null && _matchSound != null)
        {
            _audioSource.PlayOneShot(_matchSound);
        }
        
        foreach (PuzzleBlock block in matchedBlocks)
        {
            if (_matchEffectPrefab != null)
            {
                Instantiate(_matchEffectPrefab, block.blockObject.transform.position, Quaternion.identity);
            }
        }
        
        _totalMatches += matchedBlocks.Count;
        OnBlocksMatched?.Invoke(matchedBlocks.Count);
        
        // Remove matched blocks
        foreach (PuzzleBlock block in matchedBlocks)
        {
            block.blockObject.SetActive(false);
        }
        
        yield return new WaitForSeconds(0.3f);
        
        // Drop blocks down
        yield return StartCoroutine(DropBlocks());
        
        // Fill empty spaces
        FillEmptySpaces();
        
        // Check for more matches
        if (HasMatches())
        {
            yield return StartCoroutine(ProcessMatches());
        }
        else
        {
            CheckGameState();
        }
    }
    
    private IEnumerator DropBlocks()
    {
        bool blocksDropped = true;
        
        while (blocksDropped)
        {
            blocksDropped = false;
            
            for (int x = 0; x < _gridWidth; x++)
            {
                for (int y = 0; y < _gridHeight - 1; y++)
                {
                    if (!_grid[x, y].blockObject.activeInHierarchy && _grid[x, y + 1].blockObject.activeInHierarchy)
                    {
                        // Move block down
                        _grid[x, y].blockObject.SetActive(true);
                        _grid[x, y].currentColor = _grid[x, y + 1].currentColor;
                        _grid[x, y].blockRenderer.material = _grid[x, y + 1].blockRenderer.material;
                        
                        _grid[x, y + 1].blockObject.SetActive(false);
                        blocksDropped = true;
                    }
                }
            }
            
            if (blocksDropped)
            {
                yield return new