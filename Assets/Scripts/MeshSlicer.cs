using System.Collections.Generic;
using UnityEngine;
using System.Diagnostics;

public class MeshSlicer : MonoBehaviour {
    public class Triangle {
        public Vector3 a;
        public Vector3 b;
        public Vector3 c;
        public Vector3 normal;

        public Triangle(Vector3 a, Vector3 b, Vector3 c, Vector3 normal) {
            this.a = a;
            this.b = b;
            this.c = c;
            this.normal = normal;
        }
    }

    public enum DebugOption {
        NONE,
        DRAW_NORMALS,
        DRAW_VERTICES,
        DRAW_INTERIOR_POLYGON_VERTICES,
        DRAW_INTERIOR_FACES,
        DRAW_COLORIZED_VERTICES
    }

    private List<Vector3> interiorFacePoints;
    private List<Vector3> interiorFacePointsAfter;
    private List<Triangle> interiorFaceTriangles;
    MeshFilter meshfilter;
    Renderer meshRenderer;
    public DebugOption debugOption = DebugOption.NONE;


    // Start is called before the first frame update
    void OnEnable() {
        this.meshfilter = this.GetComponent<MeshFilter>();
        this.meshRenderer = this.GetComponent<MeshRenderer>();
        
    }

    void AssignFilterAndRenderer(MeshFilter meshFilter, MeshRenderer renderer) {
        this.meshfilter = meshFilter;
        this.meshRenderer = renderer;
    }

    public void Slice(Plane plane, Vector3 pointOnPlane) { 

        GameObject left = new GameObject();
        GameObject right = new GameObject();

        left.transform.name = "left";
        right.transform.name = "right";
        left.transform.position = this.transform.position;
        right.transform.position = this.transform.position;
        
        // TODO: Can probably do this cleaner, was having a problem with gameobject.instantiate since it was not making a deep copy of the 
        // underlying components
        MeshFilter leftMF = left.AddComponent<MeshFilter>();

        MeshRenderer leftRenderer = left.AddComponent<MeshRenderer>();
        leftRenderer.material = meshRenderer.material;
        MeshCollider leftMeshCollider = left.AddComponent<MeshCollider>();
        // leftMeshCollider.convex = true;
        // left.AddComponent<Rigidbody>();

        
        MeshFilter rightMF = right.AddComponent<MeshFilter>();
        MeshRenderer rightRenderer = right.AddComponent<MeshRenderer>();
        rightRenderer.material = meshRenderer.material;
        MeshCollider rightMeshCollider = right.AddComponent<MeshCollider>();
        // rightMeshCollider.convex = true;
        // right.AddComponent<Rigidbody>();

        MeshSlicer leftSlicer = left.AddComponent<MeshSlicer>();
        MeshSlicer rightSlicer = right.AddComponent<MeshSlicer>();

        leftSlicer.AssignFilterAndRenderer(leftMF, leftRenderer);
        rightSlicer.AssignFilterAndRenderer(rightMF, rightRenderer);

        Stopwatch stopWatch = new Stopwatch();
        stopWatch.Start();

        for (int i = 0; i < this.meshfilter.mesh.subMeshCount; i++) {
            int[] triangles = this.meshfilter.mesh.GetTriangles(i);

            for (int j = 0; j < triangles.Length; j+=3) {
                int a = triangles[j];
                int b = triangles[j+1];
                int c = triangles[j+2];

                bool aPosSide = plane.GetSide(meshfilter.mesh.vertices[a]);
                bool bPosSide = plane.GetSide(meshfilter.mesh.vertices[b]);
                bool cPosSide = plane.GetSide(meshfilter.mesh.vertices[c]);

                if (aPosSide && bPosSide && cPosSide) {
                    rightSlicer.AddTriangle(meshfilter.mesh.vertices[a],  meshfilter.mesh.vertices[b], meshfilter.mesh.vertices[c], meshfilter.mesh.normals[a]);
                } else if (!aPosSide && !bPosSide && !cPosSide) {
                    leftSlicer.AddTriangle(meshfilter.mesh.vertices[a], meshfilter.mesh.vertices[b], meshfilter.mesh.vertices[c], meshfilter.mesh.normals[a]);
                } else {
                    this.CutTriangle(a, aPosSide, b, bPosSide, c, cPosSide, pointOnPlane, plane, leftSlicer, rightSlicer);
                }
            }
        }

        // draw interior faces
        leftSlicer.DrawInteriorFaces(plane.normal);
        rightSlicer.DrawInteriorFaces(-plane.normal);

        leftMeshCollider.sharedMesh = leftSlicer.GetMesh();
        rightMeshCollider.sharedMesh = rightSlicer.GetMesh();

        Destroy(this.gameObject);
    }

    protected void DrawInteriorFaces(Vector3 normal) {
        if (this.interiorFacePoints == null) return;
        if (this.interiorFacePoints.Count < 3) return;

        Vector3 center = new Vector3();
        foreach(Vector3 point in interiorFacePoints) {
            center += point;
        }

        center /= interiorFacePoints.Count;
        interiorFacePointsAfter = new List<Vector3>();
        interiorFacePointsAfter.Add(center);
        if (interiorFaceTriangles == null) interiorFaceTriangles = new List<Triangle>();
        for (int i = 0; i < this.interiorFacePoints.Count; i+=2) {
            Triangle t = new Triangle(center, this.interiorFacePoints[i], this.interiorFacePoints[i+1], normal);
            interiorFaceTriangles.Add(t);
            AddTriangle(t);
        }

    }

    protected void CutTriangle(int aIdx, bool aPlaneSide, int bIdx, bool bPlaneSide, int cIdx, bool cPlaneSide, Vector3 pointOnPlane, Plane p, MeshSlicer left, MeshSlicer right) {
        int loneIdx = -1;
        bool lonePlaneSide = false;
        int p1Idx = -1;
        int p2Idx = -1;

        if (aPlaneSide == bPlaneSide) {
            loneIdx = cIdx;
            lonePlaneSide = cPlaneSide;

            p1Idx = aIdx;
            p2Idx = bIdx;
        } else if (aPlaneSide == cPlaneSide) {
            loneIdx = bIdx;
            lonePlaneSide = bPlaneSide;

            p1Idx = aIdx;
            p2Idx = cIdx;
        } else if (bPlaneSide == cPlaneSide) {
            loneIdx = aIdx;
            lonePlaneSide = aPlaneSide;

            p1Idx = bIdx;
            p2Idx = cIdx;
        }
        
        // d = (p0-l0).n / l . n
        Vector3 loneVertex = this.meshfilter.mesh.vertices[loneIdx];
        Vector3 normal = this.meshfilter.mesh.normals[loneIdx];
        Vector3 p1 = this.meshfilter.mesh.vertices[p1Idx];
        Vector3 p2 = this.meshfilter.mesh.vertices[p2Idx];
        
        Vector3 p0MinusL0 = (pointOnPlane-loneVertex);
        
        Vector3 line1 = (p1 - loneVertex);
        Vector3 line2 = (p2 - loneVertex);

        float p0MinusL0DotN = Vector3.Dot(p0MinusL0, p.normal);
        float d1 = p0MinusL0DotN / Vector3.Dot(line1, p.normal);
        float d2 = p0MinusL0DotN / Vector3.Dot(line2, p.normal);

        Vector3 i1 = loneVertex + line1*d1;
        Vector3 i2 = loneVertex + line2*d2;

        Vector3 offset = this.transform.position;
        // Debug.DrawRay(loneVertex, line1*d1, Color.magenta, 5f);
        // Debug.DrawRay(loneVertex, line2*d2, Color.green, 5f);

        Triangle t1 = new Triangle(loneVertex, i2, i1, normal);
        Triangle t2 = new Triangle(p1, i1, i2, normal);
        Triangle t3 = new Triangle(p1, i2, p2, normal);
                                    
        if (lonePlaneSide) {
            right.AddTriangle(t1);
            left.AddTriangle(t2);
            left.AddTriangle(t3);
        } else {
            left.AddTriangle(t1);
            right.AddTriangle(t2);
            right.AddTriangle(t3);
        }

        if (right.interiorFacePoints == null) right.interiorFacePoints = new List<Vector3>();
        right.interiorFacePoints.Add(i1);
        right.interiorFacePoints.Add(i2);

        if (left.interiorFacePoints == null) left.interiorFacePoints = new List<Vector3>();
        left.interiorFacePoints.Add(i1);
        left.interiorFacePoints.Add(i2);
    }

    Mesh GetMesh() {
        return this.meshfilter.mesh;
    }

    void AddTriangle(Vector3 a, Vector3 b, Vector3 c, Vector3 normal) { 

        List<Vector3> vertices = new List<Vector3>(this.meshfilter.mesh.vertices);
        List<Vector3> normals = new List<Vector3>(this.meshfilter.mesh.normals);
        if (Vector3.Dot(normal, Vector3.Cross(b - a, b-c)) < 0) {
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
        } else {
            vertices.Add(a);
            vertices.Add(c);
            vertices.Add(b);
        }

        normals.Add(normal);
        normals.Add(normal);
        normals.Add(normal);
        
        List<int> triangles = new List<int>(this.meshfilter.mesh.triangles);
        triangles.Add(vertices.Count-3);
        triangles.Add(vertices.Count-2);
        triangles.Add(vertices.Count-1);

        meshfilter.mesh.vertices = vertices.ToArray();
        meshfilter.mesh.normals = normals.ToArray();
        meshfilter.mesh.triangles = triangles.ToArray();
    }

    public void AddTriangle(Triangle t) {
        this.AddTriangle(t.a, t.b, t.c, t.normal);
    }

    private void DrawTriangle(Triangle t) {

        Vector3 AB = t.b - t.a;
        Vector3 AC = t.c - t.a;
        Vector3 BC = t.c - t.b;

        Gizmos.color = Color.red;
        Gizmos.DrawRay(t.a, AB);
        Gizmos.color = Color.green;
        Gizmos.DrawRay(t.a, AC);
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(t.b, BC);
    }

    private void OnDrawGizmosAdokako() {
        if (this.meshfilter == null) return;

        switch(this.debugOption) {
            case DebugOption.DRAW_INTERIOR_FACES:
                if (interiorFaceTriangles != null) {
                for (int i = 0;  i < interiorFaceTriangles.Count; i++) {
                    Vector3 offset = this.transform.position;
                    Triangle t = new Triangle(offset + interiorFaceTriangles[i].a, offset + interiorFaceTriangles[i].b, offset + interiorFaceTriangles[i].c, Vector3.zero);
                    DrawTriangle(t);
                }
            }
            break;
            case DebugOption.DRAW_COLORIZED_VERTICES:
                int[] triangles = this.meshfilter.mesh.GetTriangles(0);
                for (int i = 0; i < triangles.Length; i+=3) {
                    int a = triangles[i];
                    int b = triangles[i+1];
                    int c = triangles[i+2];

                    Gizmos.color = Color.red;
                    Gizmos.DrawRay(this.transform.position + this.meshfilter.mesh.vertices[a], this.meshfilter.mesh.normals[a] * .2f);                
                    Gizmos.color = Color.green;
                    Gizmos.DrawRay(this.transform.position + this.meshfilter.mesh.vertices[b], this.meshfilter.mesh.normals[b] * .2f);                
                    Gizmos.color = Color.blue;
                    Gizmos.DrawRay(this.transform.position + this.meshfilter.mesh.vertices[c], this.meshfilter.mesh.normals[c] * .2f);                
                }
                break;
            case DebugOption.DRAW_VERTICES:
                for (int i = 0; i < this.meshfilter.mesh.vertexCount; i++) {
                    Gizmos.DrawCube(this.transform.position + this.meshfilter.mesh.vertices[i], new Vector3(.05f, .05f, .05f));
                }
                break;
            case DebugOption.DRAW_INTERIOR_POLYGON_VERTICES:
                if (this.interiorFacePoints == null) return;
                Gizmos.color = Color.white;
                for (int i = 0; i < this.interiorFacePoints.Count; i++) {
                    Gizmos.DrawCube(this.transform.position + this.interiorFacePoints[i], new Vector3(.05f, .05f, .05f));
                }

                if (this.interiorFacePointsAfter != null) {
                    Gizmos.color = Color.red;
                    for (int i = 0; i < this.interiorFacePointsAfter.Count; i++) {
                        Gizmos.DrawCube(this.transform.position + this.interiorFacePointsAfter[i], new Vector3(.05f, .05f, .05f));
                    }
                }
                break;
            case DebugOption.DRAW_NORMALS:
                for (int i = 0; i < this.meshfilter.mesh.vertexCount; i++) {
                    Gizmos.color = Color.blue;
                    Gizmos.DrawRay(this.transform.position + this.meshfilter.mesh.vertices[i], this.meshfilter.mesh.normals[i] * .3f);
                }
                break;
            case DebugOption.NONE:
            default:
                return;
        }
    }

}
