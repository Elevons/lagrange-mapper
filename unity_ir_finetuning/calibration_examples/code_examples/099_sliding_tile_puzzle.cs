// Prompt: sliding tile puzzle
// Type: general

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections.Generic;
using System.Linq;

public class SlidingTilePuzzle : MonoBehaviour
{
    [System.Serializable]
    public class PuzzleTile
    {
        public GameObject tileObject;
        public int correctPosition;
        public int currentPosition;
        public bool isEmpty;
        
        public PuzzleTile(GameObject obj, int correct, int current, bool empty = false)
        {
            tileObject = obj;
            correctPosition = correct;
            currentPosition = current;
            isEmpty = empty;
        }
    }

    [Header("Puzzle Configuration")]
    [SerializeField] private int _gridSize = 3;
    [SerializeField] private float _tileSize = 1f;
    [SerializeField] private float _tileSpacing = 0.1f;
    [SerializeField] private float _slideSpeed = 5f;
    
    [Header("Tile Setup")]
    [SerializeField] private GameObject _tilePrefab;
    [SerializeField] private Transform _puzzleContainer;
    [SerializeField] private Material _tileMaterial;
    [SerializeField] private Material _emptyTileMaterial;
    
    [Header("Input")]
    [SerializeField] private LayerMask _tileLayerMask = 1;
    [SerializeField] private Camera _puzzleCamera;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _slideSFX;
    [SerializeField] private AudioClip _completeSFX;
    [SerializeField] private AudioClip _invalidMoveSFX;
    
    [Header("UI")]
    [SerializeField] private Button _shuffleButton;
    [SerializeField] private Button _solveButton;
    [SerializeField] private Text _movesText;
    [SerializeField] private Text _statusText;
    
    [Header("Events")]
    public UnityEvent OnPuzzleComplete;
    public UnityEvent OnTileSlide;
    public UnityEvent OnInvalidMove;

    private PuzzleTile[,] _grid;
    private List<PuzzleTile> _tiles;
    private Vector2Int _emptyPosition;
    private int _totalTiles;
    private int _moveCount;
    private bool _isSliding;
    private bool _isPuzzleComplete;
    private bool _isShuffling;

    private void Start()
    {
        InitializePuzzle();
        SetupUI();
        
        if (_puzzleCamera == null)
            _puzzleCamera = Camera.main;
    }

    private void Update()
    {
        if (!_isSliding && !_isShuffling && !_isPuzzleComplete)
        {
            HandleInput();
        }
    }

    private void InitializePuzzle()
    {
        _totalTiles = _gridSize * _gridSize;
        _grid = new PuzzleTile[_gridSize, _gridSize];
        _tiles = new List<PuzzleTile>();
        _moveCount = 0;
        _isPuzzleComplete = false;
        
        CreateTiles();
        PositionTiles();
        ShufflePuzzle();
    }

    private void CreateTiles()
    {
        if (_puzzleContainer == null)
        {
            GameObject container = new GameObject("Puzzle Container");
            _puzzleContainer = container.transform;
        }

        // Clear existing tiles
        foreach (Transform child in _puzzleContainer)
        {
            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);
        }

        _tiles.Clear();

        // Create tiles (leave last position empty)
        for (int i = 0; i < _totalTiles - 1; i++)
        {
            GameObject tileObj = Instantiate(_tilePrefab, _puzzleContainer);
            tileObj.name = $"Tile_{i + 1}";
            
            // Add number text
            Text numberText = tileObj.GetComponentInChildren<Text>();
            if (numberText == null)
            {
                GameObject textObj = new GameObject("Number");
                textObj.transform.SetParent(tileObj.transform);
                textObj.transform.localPosition = Vector3.zero;
                numberText = textObj.AddComponent<Text>();
                numberText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                numberText.fontSize = 24;
                numberText.alignment = TextAnchor.MiddleCenter;
                numberText.color = Color.black;
            }
            numberText.text = (i + 1).ToString();

            // Ensure collider exists
            if (tileObj.GetComponent<Collider>() == null)
            {
                BoxCollider collider = tileObj.AddComponent<BoxCollider>();
                collider.size = Vector3.one * _tileSize;
            }

            // Set material
            Renderer renderer = tileObj.GetComponent<Renderer>();
            if (renderer != null && _tileMaterial != null)
                renderer.material = _tileMaterial;

            PuzzleTile tile = new PuzzleTile(tileObj, i, i);
            _tiles.Add(tile);
        }

        // Create empty tile (invisible)
        GameObject emptyObj = new GameObject("Empty Tile");
        emptyObj.transform.SetParent(_puzzleContainer);
        if (_emptyTileMaterial != null)
        {
            MeshRenderer emptyRenderer = emptyObj.AddComponent<MeshRenderer>();
            emptyRenderer.material = _emptyTileMaterial;
            MeshFilter emptyFilter = emptyObj.AddComponent<MeshFilter>();
            emptyFilter.mesh = Resources.GetBuiltinResource<Mesh>("Cube");
            emptyObj.transform.localScale = Vector3.one * _tileSize * 0.1f; // Very small
        }

        PuzzleTile emptyTile = new PuzzleTile(emptyObj, _totalTiles - 1, _totalTiles - 1, true);
        _tiles.Add(emptyTile);
        _emptyPosition = new Vector2Int(_gridSize - 1, _gridSize - 1);
    }

    private void PositionTiles()
    {
        float startX = -(_gridSize - 1) * (_tileSize + _tileSpacing) * 0.5f;
        float startZ = -(_gridSize - 1) * (_tileSize + _tileSpacing) * 0.5f;

        for (int i = 0; i < _tiles.Count; i++)
        {
            int row = i / _gridSize;
            int col = i % _gridSize;
            
            Vector3 position = new Vector3(
                startX + col * (_tileSize + _tileSpacing),
                0,
                startZ + row * (_tileSize + _tileSpacing)
            );

            _tiles[i].tileObject.transform.localPosition = position;
            _tiles[i].currentPosition = i;
            _grid[row, col] = _tiles[i];
        }
    }

    private void HandleInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = _puzzleCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, Mathf.Infinity, _tileLayerMask))
            {
                PuzzleTile clickedTile = GetTileFromGameObject(hit.collider.gameObject);
                if (clickedTile != null && !clickedTile.isEmpty)
                {
                    TryMoveTile(clickedTile);
                }
            }
        }
    }

    private PuzzleTile GetTileFromGameObject(GameObject obj)
    {
        return _tiles.FirstOrDefault(tile => tile.tileObject == obj);
    }

    private void TryMoveTile(PuzzleTile tile)
    {
        Vector2Int tileGridPos = GetGridPosition(tile.currentPosition);
        Vector2Int emptyGridPos = _emptyPosition;

        // Check if tile is adjacent to empty space
        int distance = Mathf.Abs(tileGridPos.x - emptyGridPos.x) + Mathf.Abs(tileGridPos.y - emptyGridPos.y);
        
        if (distance == 1)
        {
            StartCoroutine(SlideTile(tile, tileGridPos, emptyGridPos));
        }
        else
        {
            PlayInvalidMoveSound();
            OnInvalidMove?.Invoke();
        }
    }

    private System.Collections.IEnumerator SlideTile(PuzzleTile tile, Vector2Int fromPos, Vector2Int toPos)
    {
        _isSliding = true;
        
        Vector3 startPosition = tile.tileObject.transform.localPosition;
        Vector3 targetPosition = GetWorldPosition(toPos);
        
        float elapsed = 0f;
        float duration = 1f / _slideSpeed;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            tile.tileObject.transform.localPosition = Vector3.Lerp(startPosition, targetPosition, t);
            yield return null;
        }

        tile.tileObject.transform.localPosition = targetPosition;

        // Update grid
        _grid[fromPos.y, fromPos.x] = _grid[toPos.y, toPos.x];
        _grid[toPos.y, toPos.x] = tile;
        
        // Update positions
        tile.currentPosition = toPos.y * _gridSize + toPos.x;
        _emptyPosition = fromPos;

        _moveCount++;
        UpdateUI();
        
        PlaySlideSound();
        OnTileSlide?.Invoke();
        
        _isSliding = false;

        if (CheckPuzzleComplete())
        {
            CompletePuzzle();
        }
    }

    private Vector2Int GetGridPosition(int index)
    {
        return new Vector2Int(index % _gridSize, index / _gridSize);
    }

    private Vector3 GetWorldPosition(Vector2Int gridPos)
    {
        float startX = -(_gridSize - 1) * (_tileSize + _tileSpacing) * 0.5f;
        float startZ = -(_gridSize - 1) * (_tileSize + _tileSpacing) * 0.5f;

        return new Vector3(
            startX + gridPos.x * (_tileSize + _tileSpacing),
            0,
            startZ + gridPos.y * (_tileSize + _tileSpacing)
        );
    }

    private bool CheckPuzzleComplete()
    {
        for (int i = 0; i < _tiles.Count; i++)
        {
            if (_tiles[i].currentPosition != _tiles[i].correctPosition)
                return false;
        }
        return true;
    }

    private void CompletePuzzle()
    {
        _isPuzzleComplete = true;
        PlayCompleteSound();
        OnPuzzleComplete?.Invoke();
        
        if (_statusText != null)
            _statusText.text = "Puzzle Complete!";
    }

    public void ShufflePuzzle()
    {
        if (_isSliding) return;
        
        StartCoroutine(ShuffleCoroutine());
    }

    private System.Collections.IEnumerator ShuffleCoroutine()
    {
        _isShuffling = true;
        _isPuzzleComplete = false;
        _moveCount = 0;

        // Perform random valid moves
        int shuffleMoves = _gridSize * _gridSize * 10;
        
        for (int i = 0; i < shuffleMoves; i++)
        {
            List<PuzzleTile> validTiles = GetValidMovableTiles();
            if (validTiles.Count > 0)
            {
                PuzzleTile randomTile = validTiles[Random.Range(0, validTiles.Count)];
                Vector2Int tilePos = GetGridPosition(randomTile.currentPosition);
                
                // Instant move without animation
                _grid[_emptyPosition.y, _emptyPosition.x] = randomTile;
                _grid[tilePos.y, tilePos.x] = _tiles.Last(); // empty tile
                
                randomTile.currentPosition = _emptyPosition.y * _gridSize + _emptyPosition.x;
                randomTile.tileObject.transform.localPosition = GetWorldPosition(_emptyPosition);
                
                _emptyPosition = tilePos;
            }
            
            if (i % 10 == 0)
                yield return null; // Yield occasionally to prevent freezing
        }

        UpdateUI();
        _isShuffling = false;
    }

    private List<PuzzleTile> GetValidMovableTiles()
    {
        List<PuzzleTile> validTiles = new List<PuzzleTile>();
        
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        
        foreach (Vector2Int dir in directions)
        {
            Vector2Int checkPos = _emptyPosition + dir;
            
            if (checkPos.x >= 0 && checkPos.x < _gridSize && checkPos.y >= 0 && checkPos.y < _gridSize)
            {
                PuzzleTile tile = _grid[checkPos.y, checkPos.x];
                if (tile != null && !tile.isEmpty)
                {
                    validTiles.Add(tile);
                }
            }
        }
        
        return validTiles;
    }

    public void SolvePuzzle()
    {
        if (_isSliding || _isShuffling) return;
        
        StartCoroutine(SolveCoroutine());
    }

    private System.Collections.IEnumerator SolveCoroutine()
    {
        _isShuffling = true;

        // Simple solve: move each tile to correct position
        for (int i = 0; i < _tiles.Count - 1; i++)
        {
            PuzzleTile tile = _tiles[i];
            Vector2Int targetPos = GetGridPosition(tile.correctPosition);
            Vector2Int currentPos = GetGridPosition(tile.currentPosition);
            
            if (currentPos != targetPos)
            {
                // Move tile to correct position instantly
                _grid[currentPos.y, currentPos.x] = _grid[targetPos.y, targetPos.x];
                _grid[targetPos.y, targetPos.x] = tile;
                
                tile.currentPosition = tile.correctPosition;
                tile.tileObject.transform.localPosition = GetWorldPosition(targetPos);
                
                yield return new WaitForSeconds(0.1f);
            }
        }

        _emptyPosition = GetGridPosition(_totalTiles - 1);
        _moveCount = 0;
        UpdateUI();
        CompletePuzzle();
        
        _isShuffling = false;
    }

    private void SetupUI()
    {
        if (_shuffleButton != null)
            _shuffleButton.onClick.AddListener(ShufflePuzzle);
            
        if (_solveButton != null)
            _solveButton.onClick.AddListener(SolvePuzzle);
            
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (_movesText != null)
            _movesText.text = $"Moves: {_moveCount}";