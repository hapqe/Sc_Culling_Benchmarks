using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class Benchmarks : MonoBehaviour
{
    [SerializeField]
    Mesh mesh;

    [SerializeField]
    int iterations = 100;

    [SerializeField]
    new Transform light;

    [SerializeField]
    Transform plane;

    Vector3[] ProjectPointsOnPlane() {
        var plane = new Plane(this.plane.up, this.plane.position);

        int count = mesh.vertexCount;
        var points = new Vector3[count];
        var vertices = mesh.vertices;

        for (int i = 0; i < count; i++) {
            var point = vertices[i];
            var projected = plane.ClosestPointOnPlane(point);
            points[i] = projected;
        }

        return points;
    }

    Vector3[] ProjectPointsOnPlaneParallel() {
        var plane = new Plane(this.plane.up, this.plane.position);

        var count = mesh.vertexCount;
        var points = new Vector3[count];
        var lightPos = light.position;
        var vertices = mesh.vertices;

        Parallel.For(0, count, i =>
        {
            points[i] = Project(plane, lightPos, vertices[i]);
        });

        return points;
    }

    private Vector3 Project(Plane plane, Vector3 lightPos, Vector3 vertex)
    {
        var ray = new Ray(lightPos, vertex - lightPos);
        if (plane.Raycast(ray, out var distance))
            return ray.GetPoint(distance);
        else return Vector3.zero;
    }

    bool IsFacing(Vector3 lightPos, Vector3 vertex, Vector3 normal) {
        var toLight = lightPos - vertex;
        var dot = Vector3.Dot(toLight, normal);
        return dot > 0;
    }

    void ProjectPointsOnPlaneAndCheckFacing() {
        var plane = new Plane(this.plane.up, this.plane.position);

        int count = mesh.vertexCount;
        var points = new Vector3[count];
        var vertices = mesh.vertices;
        var normals = mesh.normals;

        for (int i = 0; i < count; i++) {
            var facing = IsFacing(light.position, vertices[i], normals[i]);
            if (facing) {
                points[i] = Project(plane, light.position, vertices[i]);
            }
        }
    }

    void ProjectPointsOnPlaneAndCheckFacingParallel() {
        var plane = new Plane(this.plane.up, this.plane.position);

        var count = mesh.vertexCount;
        var points = new Vector3[count];
        var lightPos = light.position;
        var vertices = mesh.vertices;
        var normals = mesh.normals;

        Parallel.For(0, count, i =>
        {
            var facing = IsFacing(lightPos, vertices[i], normals[i]);
            if (facing) {
                points[i] = Project(plane, lightPos, vertices[i]);
            }
        });
    }


    void Benchmark(System.Action action, string name) {
        var watch = System.Diagnostics.Stopwatch.StartNew();

        for (int i = 0; i < iterations; i++) {
            action();
        }

        watch.Stop();
        var elapsedMs = watch.ElapsedMilliseconds;
        Debug.Log($"{name}: {elapsedMs} ms");
    }

    struct Edge {
        public int a;
        public int b;
    }

    Edge[] CalculateSilhouette() {
        var plane = new Plane(this.plane.up, this.plane.position);

        var vertices = mesh.vertices;
        var normals = mesh.normals;
        var indices = mesh.triangles;
        var applied = new Vector3[indices.Length];
        var facing = new bool[indices.Length];

        for (int i = 0; i < indices.Length; i++) {
            applied[i] = vertices[indices[i]];
            facing[i] = IsFacing(light.position, applied[i], normals[indices[i]]);
        }

        var map = new Dictionary<Vector3, List<int>>();

        for (int i = 0; i < applied.Length; i++) {
            if (!map.TryGetValue(applied[i], out var list)) {
                list = new List<int>();
                map.Add(applied[i], list);
            }
            else
            list.Add(i);
        }

        var edges = new List<Edge>();

        for (int i = 0; i < indices.Length; i += 3) {
            var a = indices[i];
            var b = indices[i + 1];
            var c = indices[i + 2];

            var ab = new Edge { a = a, b = b };
            var bc = new Edge { a = b, b = c };
            var ca = new Edge { a = c, b = a };

            void CheckEdge(Edge edge) {
                if(
                    map.TryGetValue(vertices[edge.a], out var listA) &&
                    map.TryGetValue(vertices[edge.b], out var listB) &&
                    listA.Count > 1 &&
                    listB.Count > 1
                )
                {
                    foreach (var indexA in listA) {
                        foreach (var indexB in listB) {
                            if(indexA != edge.a && indexB != edge.b) {
                                if(facing[indexA] != facing[indexB]) {
                                    edges.Add(edge);
                                }
                            }
                        }
                    }
                }
                else {
                    edges.Add(edge);
                }
            }

            CheckEdge(ab);
            CheckEdge(bc);
            CheckEdge(ca);
        }

        return edges.ToArray();
    }

    void Start()
    {
        Benchmark(() => ProjectPointsOnPlane(), "ProjectPointsOnPlane");
        Benchmark(() => ProjectPointsOnPlaneParallel(), "ProjectPointsOnPlaneParallel");
        Benchmark(() => ProjectPointsOnPlaneAndCheckFacing(), "ProjectPointsOnPlaneAndCheckFacing");
        Benchmark(() => ProjectPointsOnPlaneAndCheckFacingParallel(), "ProjectPointsOnPlaneAndCheckFacingParallel");

        // var edges = CalculateSilhouette();
        // Debug.Log($"Silhouette edges: {edges.Length}");
    }
}
