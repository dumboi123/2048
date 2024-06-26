
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using Unity.VisualScripting;
using UnityEngine;

public class GameManager : MonoBehaviour
{


    [SerializeField] private int _width = 4;
    [SerializeField] private int _height = 4;
    [SerializeField] private Node _nodePrefab;
    [SerializeField] private Block _blockPrefab;

    [SerializeField] private SpriteRenderer _boardPrefab;
    [SerializeField] private List<BlockType> _types;

    private List<Node> _nodes;
    private List<Block> _blocks;
    private GameState _state;
    private byte _round;
    private float _moveDuration = 0.2f;

    private BlockType GetBlockTypeByValue(int value) => _types.First(t => t.Value == value);
    private Node GetNodeAtPosisition(Vector2 pos) => _nodes.FirstOrDefault(n => n.Pos == pos);

    void Awake()
    {
        _round = 0;
        _nodes = new List<Node>();
        _blocks = new List<Block>();
    }

    void Start()
    {
        GenerateGrid();
        GenerateBoard();
        GenerateBlocks(4);
    }

    void Update()
    {
        if (_state != GameState.WaitingInput) return;
        if (Input.GetKeyDown(KeyCode.LeftArrow)) ShiftBlocks(Vector2.left);
        if (Input.GetKeyDown(KeyCode.RightArrow)) ShiftBlocks(Vector2.right);
        if (Input.GetKeyDown(KeyCode.UpArrow)) ShiftBlocks(Vector2.up);
        if (Input.GetKeyDown(KeyCode.DownArrow)) ShiftBlocks(Vector2.down);

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
            GenerateBlock(node, Random.value > 0.6f ? 4 : 2);

        if (freeNodes.Count() == 1)
        {
            return;
        }
        ChangeState(GameState.WaitingInput);
    }
    private void GenerateBlock(Node node, int value)
    {
        Block block = Instantiate(_blockPrefab, node.Pos, Quaternion.identity);
        block.Init(GetBlockTypeByValue(value));
        block.SetBlock(node);
        _blocks.Add(block);
    }
    private void ShiftBlocks(Vector2 direction)
    {
        ChangeState(GameState.Moving);
        List<Block> orderedBlocks = _blocks.OrderBy(b => b.Pos.x).ThenBy(b => b.Pos.y).ToList();

        if (direction == Vector2.right || direction == Vector2.up)
            orderedBlocks.Reverse();

        foreach (var block in orderedBlocks)
        {
            Node destinationNode = block.Node;
            do
            {
                block.SetBlock(destinationNode);
                var freeNode = GetNodeAtPosisition(destinationNode.Pos + direction);
                if (freeNode != null)
                {
                    if (freeNode.OccupiedBlock != null && freeNode.OccupiedBlock.CanMerge(block.Value))
                        block.MergeBlock(freeNode.OccupiedBlock);
                    else if (freeNode.OccupiedBlock == null)
                        destinationNode = freeNode;
                }
            } while (destinationNode != block.Node);
        }

        var sequence = DOTween.Sequence();
        foreach (var block in orderedBlocks)
        {
            var movePoint = block.MergingBlock != null ? block.MergingBlock.Node.Pos : block.Node.Pos;
            sequence.Insert(0, block.transform.DOMove(movePoint, _moveDuration));
        }

        sequence.OnComplete(() =>
        {
            foreach (var block in orderedBlocks.Where(block => block.MergingBlock != null))
                MergeBlocks(block.MergingBlock, block);
            ChangeState(GameState.GenerateBlocks);
        });
    }
    private void MergeBlocks(Block baseBlock, Block mergingBlock)
    {
        GenerateBlock(baseBlock.Node, baseBlock.Value * 2);

        RemoveBlock(baseBlock);
        RemoveBlock(mergingBlock);
    }
    private void RemoveBlock(Block block)
    {
        _blocks.Remove(block);
        Destroy(block.gameObject);
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
            case GameState.Win:
                break;
            case GameState.Lose:
                break;
            default:
                throw new System.ArgumentOutOfRangeException(nameof(newState), newState, null);
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
    Win,
    Lose
}
