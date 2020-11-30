using UnityEngine;
using System.Collections.Generic;
using System;

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
        DRAW_INTERIOR_FACE_VERTICES,
        DRAW_INTERIOR_FACES,
    }

    private List<Vector3> interiorFacePoints;
    private Vector3 interiorFaceCenter;
    private List<Triangle> interiorFaceTriangles;
    MeshFilter meshfilter;
    Renderer meshRenderer;
    public DebugOption debugOption = DebugOption.NONE;


    void OnEnable() {
        this.meshfilter = this.GetComponent<MeshFilter>();
        this.meshRenderer = this.GetComponent<MeshRenderer>();
    }

    public void Slice(Plane plane, Vector3 pointOnPlane) {

        GameObject left = GameObject.Instantiate(this.gameObject, this.transform.position, Quaternion.identity);
        GameObject right = GameObject.Instantiate(this.gameObject, this.transform.position, Quaternion.identity);

        left.transform.name = "left";
        right.transform.name = "right";
        
        // reset left mesh
        MeshFilter leftMF = left.GetComponent<MeshFilter>();
        leftMF.mesh = new Mesh();
        // reset right mesh
        MeshFilter rightMF = right.GetComponent<MeshFilter>();
        rightMF.mesh = new Mesh();

        // give left and right new mesh slicers
        MeshSlicer leftSlicer = left.GetComponent<MeshSlicer>();
        MeshSlicer rightSlicer = right.GetComponent<MeshSlicer>();

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
                    rightSlicer.AddTriangle(meshfilter.mesh.vertices[a], 
                                            meshfilter.mesh.vertices[b],
                                            meshfilter.mesh.vertices[c],
                                            meshfilter.mesh.normals[a]);

                } else if (!aPosSide && !bPosSide && !cPosSide) {
                    leftSlicer.AddTriangle(meshfilter.mesh.vertices[a],
                                            meshfilter.mesh.vertices[b],
                                            meshfilter.mesh.vertices[c],
                                            meshfilter.mesh.normals[a]);

                } else {
                    this.CutTriangle(a, aPosSide,
                                        b, bPosSide,
                                        c, cPosSide,
                                        pointOnPlane,
                                        plane,
                                        leftSlicer, rightSlicer);
                }
            }
        }

        // draw interior faces
        leftSlicer.DrawInteriorFaces(plane.normal);
        rightSlicer.DrawInteriorFaces(-plane.normal);

        // update collider mesh for collisions
        MeshCollider leftMeshCollider = left.GetComponent<MeshCollider>();
        MeshCollider rightMeshCollider = right.GetComponent<MeshCollider>();
        leftMeshCollider.sharedMesh = leftSlicer.meshfilter.mesh;
        rightMeshCollider.sharedMesh = rightSlicer.meshfilter.mesh;

        // destroy current game object
        Destroy(this.gameObject);
    }

    protected void DrawInteriorFaces(Vector3 normal) {
        if (this.interiorFacePoints == null) return;
        if (this.interiorFacePoints.Count < 3) return;

        if (interiorFaceTriangles == null) interiorFaceTriangles = new List<Triangle>();

        // average out interior face center
        this.interiorFaceCenter /= this.interiorFacePoints.Count;
        
        for (int i = 0; i < this.interiorFacePoints.Count; i+=2) {
            Triangle t = new Triangle(this.interiorFaceCenter, this.interiorFacePoints[i], this.interiorFacePoints[i+1], normal);
            interiorFaceTriangles.Add(t);
            AddTriangle(t);
        }
    }

    protected void CutTriangle(int aIdx, bool aPlaneSide, int bIdx, bool bPlaneSide, int cIdx, bool cPlaneSide, Vector3 hitPoint, Plane p, MeshSlicer left, MeshSlicer right) {
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
        
        Vector3 p0MinusL0 = (hitPoint-loneVertex);
        
        Vector3 line1 = (p1 - loneVertex);
        Vector3 line2 = (p2 - loneVertex);

        float p0MinusL0DotN = Vector3.Dot(p0MinusL0, p.normal);
        float d1 = p0MinusL0DotN / Vector3.Dot(line1, p.normal);
        float d2 = p0MinusL0DotN / Vector3.Dot(line2, p.normal);

        Vector3 i1 = loneVertex + line1*d1;
        Vector3 i2 = loneVertex + line2*d2;

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
        right.AddToInteriorFacePoints(i1);
        right.AddToInteriorFacePoints(i2);

        if (left.interiorFacePoints == null) left.interiorFacePoints = new List<Vector3>();
        left.AddToInteriorFacePoints(i1);
        left.AddToInteriorFacePoints(i2);
    }

    public void AddToInteriorFacePoints(Vector3 p) {
        this.interiorFacePoints.Add(p);
        this.interiorFaceCenter += p;
    }

    void AddTriangle(Vector3 a, Vector3 b, Vector3 c, Vector3 normal) { 

        Vector3[] vertices = this.meshfilter.mesh.vertices;
        Vector3[] normals = this.meshfilter.mesh.normals;
        int[] triangles = this.meshfilter.mesh.triangles;

        int vertexIndex = vertices.Length-1;
        int normalIndex = vertices.Length-1;
        int triangleIndex = triangles.Length-1;
        Array.Resize<Vector3>(ref vertices, vertices.Length+3);
        Array.Resize<Vector3>(ref normals, normals.Length+3);
        Array.Resize<int>(ref triangles, triangles.Length+3);

        if (Vector3.Dot(normal, Vector3.Cross(b - a, b-c)) < 0) {
            vertices[++vertexIndex] = a;
            vertices[++vertexIndex] = b;
            vertices[++vertexIndex] = c;
        } else {
            vertices[++vertexIndex] = a;
            vertices[++vertexIndex] = c;
            vertices[++vertexIndex] = b;
        }

        normals[++normalIndex] = normal;
        normals[++normalIndex] = normal;
        normals[++normalIndex] = normal;
        
        triangles[++triangleIndex] = vertexIndex-2;
        triangles[++triangleIndex] = vertexIndex-1;
        triangles[++triangleIndex] = vertexIndex;

        meshfilter.mesh.vertices = vertices;
        meshfilter.mesh.normals = normals;
        meshfilter.mesh.triangles = triangles;
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
        Gizmos.DrawRay(t.a, AC);
        Gizmos.DrawRay(t.b, BC);
    }

    private void OnDrawGizmos() {
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
            case DebugOption.DRAW_VERTICES:
                for (int i = 0; i < this.meshfilter.mesh.vertexCount; i++) {
                    Gizmos.DrawCube(this.transform.position + this.meshfilter.mesh.vertices[i], new Vector3(.05f, .05f, .05f));
                }
                break;
            case DebugOption.DRAW_INTERIOR_FACE_VERTICES:
                if (this.interiorFacePoints == null) return;
                Gizmos.color = Color.white;
                for (int i = 0; i < this.interiorFacePoints.Count; i++) {
                    Gizmos.DrawCube(this.transform.position + this.interiorFacePoints[i], new Vector3(.05f, .05f, .05f));
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
