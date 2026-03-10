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

        // ★ 버그 수정 (#12): 갱신 범위 제한 (최대 8타일 반경)
        int maxUpdateTiles = Mathf.RoundToInt(8f / nodeDiameter);
        startX = Mathf.Max(startX, centerNode.gridX - maxUpdateTiles);
        endX = Mathf.Min(endX, centerNode.gridX + maxUpdateTiles);
        startY = Mathf.Max(startY, centerNode.gridY - maxUpdateTiles);
        endY = Mathf.Min(endY, centerNode.gridY + maxUpdateTiles);

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
    // ★ 최적화 (#2): gCost 초기화를 O(1)로 만들기 위한 탐색 ID 패턴
    private int currentSearchId = 0;

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

        currentSearchId++; // 새 탐색 ID 발급

        // ★ 최적화 (#1): 리스트 대신 최소 힙 사용 O(log N)
        Heap<Node> openSet = new Heap<Node>(gridSizeX * gridSizeY);
        HashSet<Node> closedSet = new HashSet<Node>();
        
        // 탐색 ID 초기화
        startNode.searchId = currentSearchId;
        startNode.gCost = 0;
        startNode.hCost = GetDistance(startNode, targetNode);
        startNode.parent = null;

        openSet.Add(startNode);

        while (openSet.Count > 0)
        {
            Node currentNode = openSet.RemoveFirst();
            closedSet.Add(currentNode);

            if (currentNode == targetNode)
            {
                return RetracePath(startNode, targetNode);
            }

            foreach (Node neighbour in GetNeighbours(currentNode))
            {
                if (!neighbour.walkable || closedSet.Contains(neighbour)) 
                    continue;

                // 새로 발견된 노드면 현재 탐색 ID로 갱신 (전체 초기화 대체)
                if (neighbour.searchId != currentSearchId)
                {
                    neighbour.searchId = currentSearchId;
                    neighbour.gCost = int.MaxValue;
                    neighbour.hCost = 0;
                    neighbour.parent = null;
                }

                int newMovementCostToNeighbour = currentNode.gCost + GetDistance(currentNode, neighbour);
                if (newMovementCostToNeighbour < neighbour.gCost || !openSet.Contains(neighbour))
                {
                    neighbour.gCost = newMovementCostToNeighbour;
                    neighbour.hCost = GetDistance(neighbour, targetNode);
                    neighbour.parent = currentNode;

                    if (!openSet.Contains(neighbour))
                        openSet.Add(neighbour);
                    else
                        openSet.UpdateItem(neighbour);
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
    /// BFS 기반으로 찐 최단거리를 찾도록 수정합니다.
    /// </summary>
    private Node FindNearestWalkable(Node startNode)
    {
        Queue<Node> queue = new Queue<Node>();
        HashSet<Node> visited = new HashSet<Node>();

        queue.Enqueue(startNode);
        visited.Add(startNode);

        int maxSearchDepth = 40; // 무한 루프 방지
        int count = 0;

        while (queue.Count > 0 && count < maxSearchDepth)
        {
            Node currentNode = queue.Dequeue();
            count++;

            if (currentNode.walkable)
            {
                return currentNode;
            }

            foreach (Node neighbour in GetNeighbours(currentNode))
            {
                if (!visited.Contains(neighbour))
                {
                    visited.Add(neighbour);
                    queue.Enqueue(neighbour);
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
        
        // ★ 버그 수정 (#10): 그리드 밖의 위치일 경우 null을 반환하여 잘못된 추적 방지
        if (percentX < 0 || percentX > 1 || percentY < 0 || percentY > 1)
        {
            return null;
        }

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

    public class Node : IHeapItem<Node>
    {
        public bool walkable;
        public Vector2 worldPosition;
        public int gridX;
        public int gridY;

        public int gCost;
        public int hCost;
        public Node parent;
        
        public int searchId; // gCost 초기화 방지용 탐색 플래그
        int heapIndex;

        public Node(bool _walkable, Vector2 _worldPos, int _gridX, int _gridY)
        {
            walkable = _walkable;
            worldPosition = _worldPos;
            gridX = _gridX;
            gridY = _gridY;
        }

        public int fCost { get { return gCost + hCost; } }

        public int HeapIndex
        {
            get { return heapIndex; }
            set { heapIndex = value; }
        }

        public int CompareTo(Node nodeToCompare)
        {
            int compare = fCost.CompareTo(nodeToCompare.fCost);
            if (compare == 0)
            {
                compare = hCost.CompareTo(nodeToCompare.hCost);
            }
            return -compare; // IHeapItem은 더 높은 우선순위(낮은 Cost)를 1로 처리해야 함
        }
    }

    // ★ 성능 개선 (#1): Priority Queue(Heap) 구현
    public interface IHeapItem<T> : System.IComparable<T>
    {
        int HeapIndex { get; set; }
    }

    public class Heap<T> where T : IHeapItem<T>
    {
        T[] items;
        int currentItemCount;

        public Heap(int maxHeapSize)
        {
            items = new T[maxHeapSize];
        }

        public void Add(T item)
        {
            item.HeapIndex = currentItemCount;
            items[currentItemCount] = item;
            SortUp(item);
            currentItemCount++;
        }

        public T RemoveFirst()
        {
            T firstItem = items[0];
            currentItemCount--;
            items[0] = items[currentItemCount];
            items[0].HeapIndex = 0;
            SortDown(items[0]);
            return firstItem;
        }

        public void UpdateItem(T item)
        {
            SortUp(item);
        }

        public int Count
        {
            get { return currentItemCount; }
        }

        public bool Contains(T item)
        {
            return Equals(items[item.HeapIndex], item);
        }

        void SortDown(T item)
        {
            while (true)
            {
                int childIndexLeft = item.HeapIndex * 2 + 1;
                int childIndexRight = item.HeapIndex * 2 + 2;
                int swapIndex = 0;

                if (childIndexLeft < currentItemCount)
                {
                    swapIndex = childIndexLeft;

                    if (childIndexRight < currentItemCount)
                    {
                        if (items[childIndexLeft].CompareTo(items[childIndexRight]) < 0)
                        {
                            swapIndex = childIndexRight;
                        }
                    }

                    if (item.CompareTo(items[swapIndex]) < 0)
                    {
                        Swap(item, items[swapIndex]);
                    }
                    else
                    {
                        return;
                    }
                }
                else
                {
                    return;
                }
            }
        }

        void SortUp(T item)
        {
            int parentIndex = (item.HeapIndex - 1) / 2;

            while (true)
            {
                T parentItem = items[parentIndex];
                if (item.CompareTo(parentItem) > 0)
                {
                    Swap(item, parentItem);
                }
                else
                {
                    break;
                }
                parentIndex = (item.HeapIndex - 1) / 2;
            }
        }

        void Swap(T itemA, T itemB)
        {
            items[itemA.HeapIndex] = itemB;
            items[itemB.HeapIndex] = itemA;
            int itemAIndex = itemA.HeapIndex;
            itemA.HeapIndex = itemB.HeapIndex;
            itemB.HeapIndex = itemAIndex;
        }
    }
}
