using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// AStarManager
/// 탑다운 맵을 그리드로 쪼개어 A* 연산을 수행하는 2D 길찾기 매니저
/// 게임오브젝트 하나에 달아두고 맵 크기를 알맞게 조절합니다.
/// </summary>
public class AStarManager : MonoBehaviour
{
    public static AStarManager Instance { get; private set; }

    [Header("그리드 설정")]
    public Vector2 gridWorldSize = new Vector2(40, 40); // 계산할 맵 전체 크기
    public float nodeRadius = 0.5f;                     // 그리드 한 칸(타일)의 반지름
    public LayerMask obstacleMask;                      // 벽, 나무, 바위 등 장애물 레이어

    private Node[,] grid;
    private float nodeDiameter;
    private int gridSizeX, gridSizeY;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // 그리드 사이즈 초기화
        nodeDiameter = nodeRadius * 2;
        gridSizeX = Mathf.RoundToInt(gridWorldSize.x / nodeDiameter);
        gridSizeY = Mathf.RoundToInt(gridWorldSize.y / nodeDiameter);
        
        CreateGrid();
    }

    /// <summary>
    /// 게임 시작 시 단 1번 호출되어 맵 전체의 벽/바닥 데이터를 타일 배열로 스캔해둡니다.
    /// </summary>
    public void CreateGrid()
    {
        grid = new Node[gridSizeX, gridSizeY];
        Vector2 worldBottomLeft = (Vector2)transform.position - Vector2.right * gridWorldSize.x / 2 - Vector2.up * gridWorldSize.y / 2;

        for (int x = 0; x < gridSizeX; x++)
        {
            for (int y = 0; y < gridSizeY; y++)
            {
                // 타일의 월드 좌표
                Vector2 worldPoint = worldBottomLeft + Vector2.right * (x * nodeDiameter + nodeRadius) + Vector2.up * (y * nodeDiameter + nodeRadius);
                
                // 해당 좌표에 장애물이 있는지 확인 (반경의 90%만 체크하여 틈새 허용)
                bool walkable = !(Physics2D.OverlapCircle(worldPoint, nodeRadius * 0.9f, obstacleMask));
                
                grid[x, y] = new Node(walkable, worldPoint, x, y);
            }
        }
    }

    /// <summary>
    /// 지정된 월드 좌표 반경 내의 타일들만 물리 갱신을 수행해 렉을 최소화하며 그리드를 최신 상태로 유지합니다.
    /// </summary>
    public void UpdateGridRegion(Vector2 centerPosition, float radius)
    {
        if (grid == null) return;

        Node centerNode = NodeFromWorldPoint(centerPosition);
        int updateTilesX = Mathf.RoundToInt(radius / nodeDiameter);
        int updateTilesY = Mathf.RoundToInt(radius / nodeDiameter);

        int startX = Mathf.Clamp(centerNode.gridX - updateTilesX, 0, gridSizeX - 1);
        int endX = Mathf.Clamp(centerNode.gridX + updateTilesX, 0, gridSizeX - 1);
        int startY = Mathf.Clamp(centerNode.gridY - updateTilesY, 0, gridSizeY - 1);
        int endY = Mathf.Clamp(centerNode.gridY + updateTilesY, 0, gridSizeY - 1);

        for (int x = startX; x <= endX; x++)
        {
            for (int y = startY; y <= endY; y++)
            {
                // 타일의 월드 좌표 가져오기
                Vector2 worldPoint = grid[x, y].worldPosition;
                // 해당 좌표에 장애물이 있는지 확인 (반경의 90%만 체크하여 틈새 허용)
                bool walkable = !(Physics2D.OverlapCircle(worldPoint, nodeRadius * 0.9f, obstacleMask));
                grid[x, y].walkable = walkable;
            }
        }
    }

    /// <summary>
    /// 시작점에서 목표점까지 4방향을 바탕으로 길을 찾아 노드의 좌표 리스트를 반환합니다.
    /// </summary>
    public List<Vector2> GetPath(Vector2 startPos, Vector2 targetPos)
    {
        Node startNode = NodeFromWorldPoint(startPos);
        Node targetNode = NodeFromWorldPoint(targetPos);

        // 범위를 벗어난 경우 무산
        if (startNode == null || targetNode == null) return null;

        // 시작점이나 목표점이 장애물 위에 있으면 가장 가까운 걸을 수 있는 노드를 찾음
        if (!startNode.walkable) startNode = FindNearestWalkable(startNode);
        if (!targetNode.walkable) targetNode = FindNearestWalkable(targetNode);
        if (startNode == null || targetNode == null) return null;

        // gCost/hCost/parent 초기화 (이전 탐색 잔여 데이터 방지)
        for (int x = 0; x < gridSizeX; x++)
            for (int y = 0; y < gridSizeY; y++)
            {
                grid[x, y].gCost = int.MaxValue;
                grid[x, y].hCost = 0;
                grid[x, y].parent = null;
            }
        startNode.gCost = 0;
        startNode.hCost = GetDistance(startNode, targetNode);

        List<Node> openSet = new List<Node>();
        HashSet<Node> closedSet = new HashSet<Node>();
        openSet.Add(startNode);

        while (openSet.Count > 0)
        {
            Node currentNode = openSet[0];
            for (int i = 1; i < openSet.Count; i++)
            {
                if (openSet[i].fCost < currentNode.fCost || openSet[i].fCost == currentNode.fCost && openSet[i].hCost < currentNode.hCost)
                {
                    currentNode = openSet[i];
                }
            }

            openSet.Remove(currentNode);
            closedSet.Add(currentNode);

            if (currentNode == targetNode)
            {
                return RetracePath(startNode, targetNode);
            }

            foreach (Node neighbour in GetNeighbours(currentNode))
            {
                if (!neighbour.walkable || closedSet.Contains(neighbour)) 
                    continue;

                int newMovementCostToNeighbour = currentNode.gCost + GetDistance(currentNode, neighbour);
                if (newMovementCostToNeighbour < neighbour.gCost || !openSet.Contains(neighbour))
                {
                    neighbour.gCost = newMovementCostToNeighbour;
                    neighbour.hCost = GetDistance(neighbour, targetNode);
                    neighbour.parent = currentNode;

                    if (!openSet.Contains(neighbour))
                        openSet.Add(neighbour);
                }
            }
        }
        return null; // 길 못 찾음
    }

    private List<Vector2> RetracePath(Node startNode, Node endNode)
    {
        List<Vector2> path = new List<Vector2>();
        Node currentNode = endNode;

        while (currentNode != startNode)
        {
            path.Add(currentNode.worldPosition);
            currentNode = currentNode.parent;
        }
        path.Reverse();
        return path;
    }

    private int GetDistance(Node nodeA, Node nodeB)
    {
        int dstX = Mathf.Abs(nodeA.gridX - nodeB.gridX);
        int dstY = Mathf.Abs(nodeA.gridY - nodeB.gridY);

        // 4방향 십자(수직/수평) 이동만 허용 (비용 10). 대각선 허용 안 함.
        return 10 * (dstX + dstY);
    }
    
    /// <summary>
    /// 주어진 노드가 장애물 위에 있을 때, 가장 가까운 걸을 수 있는 노드를 찾아 반환합니다.
    /// </summary>
    private Node FindNearestWalkable(Node node)
    {
        // 나선형으로 바깥으로 탐색 (최대 5칸까지)
        for (int radius = 1; radius <= 5; radius++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    if (Mathf.Abs(dx) != radius && Mathf.Abs(dy) != radius) continue; // 테두리만 검사
                    
                    int checkX = node.gridX + dx;
                    int checkY = node.gridY + dy;
                    
                    if (checkX >= 0 && checkX < gridSizeX && checkY >= 0 && checkY < gridSizeY)
                    {
                        if (grid[checkX, checkY].walkable)
                            return grid[checkX, checkY];
                    }
                }
            }
        }
        return null; // 주변에 걸을 수 있는 타일이 없음
    }

    private List<Node> GetNeighbours(Node node)
    {
        List<Node> neighbours = new List<Node>();

        // 4방향 (상, 하, 좌, 우)
        int[] dx = { 0, 0, -1, 1 };
        int[] dy = { 1, -1, 0, 0 };

        for(int i = 0; i < 4; i++)
        {
            int checkX = node.gridX + dx[i];
            int checkY = node.gridY + dy[i];

            if (checkX >= 0 && checkX < gridSizeX && checkY >= 0 && checkY < gridSizeY)
            {
                neighbours.Add(grid[checkX, checkY]);
            }
        }
        return neighbours;
    }

    public Node NodeFromWorldPoint(Vector2 worldPosition)
    {
        float percentX = (worldPosition.x - transform.position.x + gridWorldSize.x / 2) / gridWorldSize.x;
        float percentY = (worldPosition.y - transform.position.y + gridWorldSize.y / 2) / gridWorldSize.y;
        
        percentX = Mathf.Clamp01(percentX);
        percentY = Mathf.Clamp01(percentY);

        int x = Mathf.RoundToInt((gridSizeX - 1) * percentX);
        int y = Mathf.RoundToInt((gridSizeY - 1) * percentY);
        return grid[x, y];
    }
    
    void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireCube(transform.position, new Vector3(gridWorldSize.x, gridWorldSize.y, 1));
        if (grid != null)
        {
            foreach (Node n in grid)
            {
                Gizmos.color = n.walkable ? new Color(1, 1, 1, 0.1f) : new Color(1, 0, 0, 0.5f);
                Gizmos.DrawCube(n.worldPosition, Vector3.one * (nodeDiameter - 0.1f));
            }
        }
    }

    public class Node
    {
        public bool walkable;
        public Vector2 worldPosition;
        public int gridX;
        public int gridY;

        public int gCost;
        public int hCost;
        public Node parent;

        public Node(bool _walkable, Vector2 _worldPos, int _gridX, int _gridY)
        {
            walkable = _walkable;
            worldPosition = _worldPos;
            gridX = _gridX;
            gridY = _gridY;
        }

        public int fCost { get { return gCost + hCost; } }
    }
}
