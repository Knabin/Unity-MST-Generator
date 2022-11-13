using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;
using Graphs;

public class DungeonGenerator : MonoBehaviour {
    enum CellType {
        None,
        Room,
        Hallway
    }

    class Room {
        public RectInt bounds;

        public Room(Vector2Int location, Vector2Int size) {
            bounds = new RectInt(location, size);
        }

        public static bool Intersect(Room a, Room b) {
            return !((a.bounds.position.x >= (b.bounds.position.x + b.bounds.size.x)) || ((a.bounds.position.x + a.bounds.size.x) <= b.bounds.position.x)
                || (a.bounds.position.y >= (b.bounds.position.y + b.bounds.size.y)) || ((a.bounds.position.y + a.bounds.size.y) <= b.bounds.position.y));
        }
    }

    [SerializeField]
    Vector2Int size;
    [SerializeField]
    int roomCount;
    [SerializeField]
    Vector2Int roomMinSize;
    [SerializeField]
    Vector2Int roomMaxSize;
    [SerializeField]
    Vector2Int bossRoomMinSize;
    [SerializeField]
    Vector2Int bossRoomMaxSize;
    [SerializeField]
    GameObject cubePrefab;
    [SerializeField]
    GameObject hallPrefab;
    [SerializeField]
    Material redMaterial;
    [SerializeField]
    Material blueMaterial;
    [SerializeField]
    Material startMaterial;
    [SerializeField]
    Material bossMaterial;

    Random random;
    Grid<CellType> grid;
    List<Room> rooms;
    List<Vector2Int> doors;
    Room bossRoom;
    Delaunay delaunay;
    HashSet<Prim.Edge> selectedEdges;

    void Start() {
        Generate();
    }

    void Generate() {
        random = new Random(UnityEngine.Random.Range(0, int.MaxValue));
        grid = new Grid<CellType>(size, Vector2Int.zero);
        rooms = new List<Room>();
        doors = new List<Vector2Int>();

        if (PlaceRooms())
        {
            Triangulate();
            CreateHallways();
            PathfindHallways();
        }
    }

    bool PlaceRooms() {
        for (int i = 0; i < roomCount; i++) {
            Vector2Int location = new Vector2Int(
                random.Next(0, size.x),
                random.Next(0, size.y)
            );

            Vector2Int roomSize = new Vector2Int(
                random.Next(roomMinSize.x, roomMaxSize.x + 1),
                random.Next(roomMinSize.y, roomMaxSize.y + 1)
            );

            bool add = true;
            Room newRoom = new Room(location, roomSize);
            Room buffer = new Room(location + new Vector2Int(-1, -1), roomSize + new Vector2Int(2, 2));

            foreach (var room in rooms) {
                if (Room.Intersect(room, buffer)) {
                    add = false;
                    break;
                }
            }

            if (newRoom.bounds.xMin < 0 || newRoom.bounds.xMax >= size.x
                || newRoom.bounds.yMin < 0 || newRoom.bounds.yMax >= size.y) {
                add = false;
            }

            if (add) {
                rooms.Add(newRoom);

                foreach (var pos in newRoom.bounds.allPositionsWithin) {
                    grid[pos] = CellType.Room;
                }
            }
        }

        // 첫 번째 방을 startRoom으로 지정한다.
        Room startRoom = rooms[0];
        float test = float.MinValue;

        // 거리가 가장 먼 방을 Boss Room으로 설정한다.
        foreach (var room in rooms)
        {
            float test2 = Vector2Int.Distance(Vector2Int.RoundToInt(startRoom.bounds.center), Vector2Int.RoundToInt(room.bounds.center));
            if (test < test2)
            {
                test = test2;
                bossRoom = room;
            }
        }

        // 최소 사이즈 이상으로 키워 보고 못 만들면 다시 만든다.
        if (bossRoom.bounds.size.x < bossRoomMinSize.x || bossRoom.bounds.size.y < bossRoomMinSize.y)
        {
            Room buffer = new Room(bossRoom.bounds.position, bossRoomMinSize);

            foreach(var room in rooms)
            {
                if (Room.Intersect(room, buffer)) {
                    Debug.Log("겹쳐용!");
                    Generate();
                    return false;
                }
            }

            bossRoom.bounds.size = bossRoomMinSize;
        }

        foreach (var room in rooms)
        {
            //PlaceRoom(room.bounds.position, room.bounds.size, room == rooms[0], room == bossRoom);
            PlaceRooms(room.bounds.position, room.bounds.size);
        }

        return true;
    }

    void Triangulate() {
        List<Vertex> vertices = new List<Vertex>();

        foreach (var room in rooms) {
            vertices.Add(new Vertex<Room>((Vector2)room.bounds.position + ((Vector2)room.bounds.size) / 2, room));
        }

        delaunay = Delaunay.Triangulate(vertices);
    }

    void CreateHallways() {
        List<Prim.Edge> edges = new List<Prim.Edge>();

        foreach (var edge in delaunay.Edges) {
            edges.Add(new Prim.Edge(edge.U, edge.V));
        }

        List<Prim.Edge> mst = Prim.MinimumSpanningTree(edges, edges[0].U);

        selectedEdges = new HashSet<Prim.Edge>(mst);
        var remainingEdges = new HashSet<Prim.Edge>(edges);
        remainingEdges.ExceptWith(selectedEdges);

        foreach (var edge in remainingEdges) {
            if (random.NextDouble() < 0.125) {
                selectedEdges.Add(edge);
            }
        }

        int count = 0;
        List<Prim.Edge> deleteEdge = new List<Prim.Edge>();

        // 보스방은 입구를 하나만 만들어 놓는다.
        foreach (var edge in selectedEdges)
        {
            if (bossRoom.bounds.Contains(Vector2Int.RoundToInt(edge.V.Position)) ||
                bossRoom.bounds.Contains(Vector2Int.RoundToInt(edge.U.Position)))
            {
                if (count >= 1)
                {
                    deleteEdge.Add(edge);
                }
                else
                {
                    count++;
                }
            }
        }

        foreach (var item in deleteEdge)
        {
            selectedEdges.Remove(item);
        }
    }

    void PathfindHallways() {
        DungeonPathfinder aStar = new DungeonPathfinder(size);

        foreach (var edge in selectedEdges) {
            var startRoom = (edge.U as Vertex<Room>).Item;
            var endRoom = (edge.V as Vertex<Room>).Item;

            var startPosf = startRoom.bounds.center;
            var endPosf = endRoom.bounds.center;
            var startPos = new Vector2Int((int)startPosf.x, (int)startPosf.y);
            var endPos = new Vector2Int((int)endPosf.x, (int)endPosf.y);

            var path = aStar.FindPath(startPos, endPos, (DungeonPathfinder.Node a, DungeonPathfinder.Node b) => {
                var pathCost = new DungeonPathfinder.PathCost();
                
                pathCost.cost = Vector2Int.Distance(b.Position, endPos);    //heuristic

                if (grid[b.Position] == CellType.Room) {
                    pathCost.cost += 10;
                } else if (grid[b.Position] == CellType.None) {
                    pathCost.cost += 5;
                } else if (grid[b.Position] == CellType.Hallway) {
                    pathCost.cost += 1;
                }

                pathCost.traversable = true;

                return pathCost;
            });

            if (path != null) {
                bool first = false;
                Vector2Int last = Vector2Int.zero;
                for (int i = 0; i < path.Count; i++) {
                    var current = path[i];

                    if (grid[current] == CellType.None) {
                        grid[current] = CellType.Hallway;

                        if (first == false)
                        {
                            // startpos 쪽으로 한 칸 이동?
                            doors.Add(current);
                            first = true;
                        }

                        last = current;
                    }

                    if (i > 0) {
                        var prev = path[i - 1];

                        var delta = current - prev;
                    }
                }

                if (last != Vector2Int.zero)
                {
                    // endpos 쪽으로 한 칸 이동?
                    doors.Add(last);
                }
            }
        }

        for (int i = 0; i < grid.Size.x; ++i)
        {
            for (int j = 0; j < grid.Size.y; ++j)
            {
                if (grid[i, j] == CellType.Hallway) {
                    PlaceHallway(new Vector2Int(i, j));
                }
            }
        }

        foreach (var door in doors)
        {
            Debug.Log(door);
        }
    }
    static int roomCounts = 1;
    static int hallwayCounts = 1;

    void PlaceRoom(Vector2Int location, Vector2Int size, Material material) {
        GameObject go = Instantiate(cubePrefab, new Vector3(location.x, 0, location.y), Quaternion.identity);
        go.name = "Room " + roomCounts++;
        go.GetComponent<Transform>().localScale = new Vector3(size.x, 1, size.y);
        //go.GetComponent<MeshRenderer>().material = material;

        RoomScript test = go.GetComponent<RoomScript>();
        if (grid[location.x, location.y + 1] == CellType.Room)
        {
            if (test) {
                test.Test(RoomScript.WallType.LEFT);
            }
        }
        if (grid[location.x, location.y - 1] == CellType.Room)
        {
            if (test) {
                test.Test(RoomScript.WallType.RIGHT);
            }
        }
        if (grid[location.x - 1, location.y] == CellType.Room)
        {
            if (test) {
                test.Test(RoomScript.WallType.BOTTOM);
            }
        }
        if (grid[location.x + 1, location.y] == CellType.Room)
        {
            if (test) {
                test.Test(RoomScript.WallType.TOP);
            }
        }
    }

    void PlaceHallway(Vector2Int location, Vector2Int size, Material material) {
        GameObject go = Instantiate(hallPrefab, new Vector3(location.x, 0, location.y), Quaternion.identity);
        go.name = "Hallway " + hallwayCounts++;
        go.GetComponent<Transform>().localScale = new Vector3(size.x, 1, size.y);

        RoomScript test = go.GetComponent<RoomScript>();
        if (grid[location.x, location.y + 1] == CellType.Hallway)
        {
            if (test) {
                test.Test(RoomScript.WallType.LEFT);
            }
        }
        if (grid[location.x, location.y - 1] == CellType.Hallway)
        {
            if (test) {
                test.Test(RoomScript.WallType.RIGHT);
            }
        }
        if (grid[location.x - 1, location.y] == CellType.Hallway)
        {
            if (test) {
                test.Test(RoomScript.WallType.BOTTOM);
            }
        }
        if (grid[location.x + 1, location.y] == CellType.Hallway)
        {
            if (test) {
                test.Test(RoomScript.WallType.TOP);
            }
        }
    }

    void PlaceRoom(Vector2Int location, Vector2Int size, bool isStartRoom = false, bool isBossRoom = false) {
        PlaceRoom(location, size, isStartRoom ? startMaterial : isBossRoom ? bossMaterial : redMaterial);
    }

    void PlaceRooms(Vector2Int location, Vector2Int size) {
        for (int i = 0; i < size.x; ++i)
        {
            for (int j = 0; j < size.y; ++j)
            {
                // TODO : Out of index 처리.. 뭔가 이상하게 동작 중
                //PlaceRoom(location + new Vector2Int(i, j), Vector2Int.one);
                PlaceRoom(new Vector2Int(
                    Math.Max(size.x, location.x + i),
                    Math.Max(size.y, location.y + j)), Vector2Int.one);
            }
        }
    }

    void PlaceHallway(Vector2Int location) {
        PlaceHallway(location, new Vector2Int(1, 1), blueMaterial);
    }
}
