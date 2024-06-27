
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{

    [Header("Prefabs")]
    [SerializeField] private Node _nodePrefab;
    [SerializeField] private Block _blockPrefab;
    [SerializeField] private SpriteRenderer _boardPrefab;

    [Space(10)]
    //====================================================
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI _textScoreUI;
    [SerializeField] private TextMeshProUGUI _textBestScoreUI;
    [SerializeField] private Button _buttonNew;
    [SerializeField] private GameObject _buttonTryAgain;
    [SerializeField] private Image _blurredScreen;
    [SerializeField] private TextMeshProUGUI _textGameOver;

    [Space(10)]
    //====================================================

    [SerializeField] private List<BlockType> _types;



    private List<Node> _nodes;
    private List<Block> _blocks;
    private GameState _state;
    private byte _round;
    private int _width, _height, _score, _bestScore;
    private float _moveDuration = 0.2f;

    void Awake()
    {
        Screen.SetResolution(400,710,false);
        CheckPlayerPrefs();
        _score = 0;
        _round = 0;
        _width = 4; _height = 4;
        _nodes = new List<Node>();
        _blocks = new List<Block>();
    }

    void Start()
    {
        SetUI();
        GenerateGrid();
        GenerateBoard();
        GenerateBlocks(3);
    }

    void Update()
    {
        if (_state != GameState.WaitingInput) return;
        HandleInput();
    }
    //=========================================SETTING UP=============================
    private void CheckPlayerPrefs()
    {
        if (PlayerPrefs.HasKey("BestScore"))
            _bestScore = PlayerPrefs.GetInt("BestScore");
        else
            _bestScore = 0;
    }
    private void SetUI()
    {
        _textScoreUI.text = _score.ToString();
        _textBestScoreUI.text = _bestScore.ToString();
    }
    private void GenerateGrid()
    {
        for (int i = 0; i < _width; i++)
        {
            for (int j = 0; j < _height; j++)
            {
                Node node = Instantiate(_nodePrefab, new Vector2(i, j), Quaternion.identity);
                _nodes.Add(node);
            }
        }
    }
    private void GenerateBoard()
    {
        //set board position
        var center = new Vector2((float)_width / 2 - 0.5f, (float)_height / 2 - 0.5f);
        var board = Instantiate(_boardPrefab, center, Quaternion.identity);
        board.size = new Vector2(_width, _height);
        //set Camera
        Camera.main.transform.position = new Vector3(center.x, center.y, -10);
    }
    private void GenerateBlocks(int amount)
    {
        List<Node> freeNodes = _nodes.Where(n => n.OccupiedBlock == null).OrderBy(b => Random.value).ToList();
        foreach (var node in freeNodes.Take(amount))
            GenerateBlock(node, Random.value > 0.7f ? 4 : 2);

        if (freeNodes.Count() == 1 && !CheckSurrounding())
        {
            ChangeState(GameState.Lose);
            return;
        }
        ChangeState(GameState.WaitingInput);
    }
    private bool CheckSurrounding()
    {
        foreach (var block in _blocks)
        {
            Node destinationNode = block.Node;
            block.SetBlock(destinationNode);

            var freeNodeLeft = GetNodeAtPosisition(destinationNode.Pos + Vector2.left);
            var freeNodeRight = GetNodeAtPosisition(destinationNode.Pos + Vector2.right);
            var freeNodeUp = GetNodeAtPosisition(destinationNode.Pos + Vector2.up);
            var freeNodeDown = GetNodeAtPosisition(destinationNode.Pos + Vector2.down);

            if (CheckFreeNode(freeNodeLeft, block) || CheckFreeNode(freeNodeRight, block) || CheckFreeNode(freeNodeUp, block) || CheckFreeNode(freeNodeDown, block))
                return true;
        }
        return false;
    }
    private bool CheckFreeNode(Node freeNode, Block block)
    {
        bool condition = false;
        if (freeNode != null)
        {
            if (freeNode.OccupiedBlock != null && freeNode.OccupiedBlock.CanMerge(block.Value))
                condition = true;
        }
        return condition;
    }

    private void GenerateBlock(Node node, int value)
    {
        Block block = Instantiate(_blockPrefab, node.Pos, Quaternion.identity);
        block.Init(GetBlockTypeByValue(value));
        block.SetBlock(node);
        _blocks.Add(block);
    }

    //=========================================GAME LOGIC=============================
    private BlockType GetBlockTypeByValue(int value) => _types.First(t => t.Value == value);
    private Node GetNodeAtPosisition(Vector2 pos) => _nodes.FirstOrDefault(n => n.Pos == pos);

    private void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.LeftArrow)) ShiftBlocks(Vector2.left);
        else if (Input.GetKeyDown(KeyCode.RightArrow)) ShiftBlocks(Vector2.right);
        else if (Input.GetKeyDown(KeyCode.UpArrow)) ShiftBlocks(Vector2.up);
        else if (Input.GetKeyDown(KeyCode.DownArrow)) ShiftBlocks(Vector2.down);
    }

    private void ShiftBlocks(Vector2 direction)
    {
        ChangeState(GameState.Moving);
        List<Block> orderedBlocks = _blocks.OrderBy(b => b.Pos.x).ThenBy(b => b.Pos.y).ToList();

        if (direction == Vector2.right || direction == Vector2.up)
            orderedBlocks.Reverse();

        bool checkChange = CheckBlocks(orderedBlocks, direction);
        HandleAnimation(orderedBlocks, checkChange);
    }
    private bool CheckBlocks(List<Block> blockList, Vector2 direction)
    {
        bool isChanged = false;
        foreach (var block in blockList)
        {
            Node destinationNode = block.Node;
            do
            {
                block.SetBlock(destinationNode);
                var freeNode = GetNodeAtPosisition(destinationNode.Pos + direction);
                if (freeNode != null)
                {
                    if (freeNode.OccupiedBlock != null && freeNode.OccupiedBlock.CanMerge(block.Value))
                    {
                        isChanged = true;
                        block.MergeBlock(freeNode.OccupiedBlock);
                    }
                    else if (freeNode.OccupiedBlock == null)
                    {
                        isChanged = true;
                        destinationNode = freeNode;
                    }
                }
            } while (destinationNode != block.Node);
        }
        return isChanged;
    }
    private void HandleAnimation(List<Block> blockList, bool checkChange)
    {
        if (checkChange)
        {
            var sequence = DOTween.Sequence();
            //Add animation
            foreach (var block in blockList)
            {
                var movePoint = block.MergingBlock != null ? block.MergingBlock.Node.Pos : block.Node.Pos;
                sequence.Insert(0, block.transform.DOMove(movePoint, _moveDuration));
            }
            //Run animation once it completed
            sequence.OnComplete(() =>
            {
                foreach (var block in blockList.Where(block => block.MergingBlock != null))
                    MergeBlocks(block.MergingBlock, block);

                ChangeState(GameState.GenerateBlocks);
                _textScoreUI.text = _score.ToString();
            });
        }
        else ChangeState(GameState.WaitingInput);
    }
    private void MergeBlocks(Block baseBlock, Block mergingBlock)
    {
        _score++;
        GenerateBlock(baseBlock.Node, baseBlock.Value * 2);

        RemoveBlock(baseBlock);
        RemoveBlock(mergingBlock);
    }
    private void RemoveBlock(Block block)
    {
        _blocks.Remove(block);
        Destroy(block.gameObject);
    }
    //=========================================GAME OVER=============================

    private void GameOver()
    {
        _buttonTryAgain.SetActive(true);
        _blurredScreen.enabled = true;
        _textGameOver.enabled = true;
    }
    private void ChangeState(GameState newState)
    {
        _state = newState;
        switch (_state)
        {
            case GameState.GenerateLevel:
                GenerateGrid();
                break;
            case GameState.GenerateBlocks:
                GenerateBlocks(_round++ == 0 ? 2 : 1);
                break;
            case GameState.WaitingInput:
                break;
            case GameState.Moving:
                break;
            case GameState.Lose:
                GameOver();
                break;
            default:
                throw new System.ArgumentOutOfRangeException(nameof(newState), newState, null);
        }
    }

    public void NewGame()
    {
        CheckBestScore();
        SceneManager.LoadScene("SampleScene");
    }
    private void CheckBestScore()
    {
        CheckPlayerPrefs();
        if (_score > _bestScore)
        {
            _bestScore = _score;
            PlayerPrefs.SetInt("BestScore", _bestScore);
        }
    }
}

[System.Serializable]
public struct BlockType
{
    public int Value;
    public Color Color;
}

public enum GameState
{
    GenerateLevel,
    GenerateBlocks,
    WaitingInput,
    Moving,
    Lose
}
