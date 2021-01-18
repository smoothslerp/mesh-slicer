﻿using UnityEngine;
using System.Collections.Generic;
using System;

public class MeshSlicer : MonoBehaviour {
    public struct Triangle {
        public Vector3 a;
        public Vector3 b;
        public Vector3 c;
        public Vector3 aNormal;
        public Vector3 bNormal;
        public Vector3 cNormal;
        public Vector2 uva;
        public Vector2 uvb;
        public Vector2 uvc;

        public Triangle(Vector3 a, Vector3 b, Vector3 c,
                        Vector3 aNormal = new Vector3(), Vector3 bNormal = new Vector3(), Vector3 cNormal = new Vector3(),
                        Vector2 uva = new Vector2(), Vector2 uvb = new Vector2(), Vector2 uvc = new Vector2()) {
            
            this.a = a;
            this.b = b;
            this.c = c;
            this.aNormal = aNormal;
            this.bNormal = bNormal;
            this.cNormal = cNormal;
            this.uva = uva;
            this.uvb = uvb;
            this.uvc = uvc;
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

    List<Vector3> newVertices;
    List<Vector3> newNormals;
    List<Vector2> newUV;
    Dictionary<int, List<int>> triangles;

    public DebugOption debugOption = DebugOption.NONE;
    public Material innerMaterial;

    public ComputeShader shader;


    void OnEnable() {
        this.meshfilter = this.GetComponent<MeshFilter>();
        this.meshRenderer = this.GetComponent<MeshRenderer>();

        this.newVertices = new List<Vector3>();
        this.newNormals = new List<Vector3>();
        this.newUV = new List<Vector2>();
        this.triangles = new Dictionary<int, List<int>>();
    }

    struct PlaneBufferStruct {
        Vector3 Normal;
        Vector3 Point;

        public PlaneBufferStruct(Vector3 Normal, Vector3 Point) {
            this.Normal = Normal;
            this.Point = Point;
            
        }
    }

    struct PointBufferStruct {
        Vector3 Point;
        public uint Side;

        public PointBufferStruct(Vector3 Point) {
            this.Point = Point;
            this.Side = 0;
        }
    }

    PointBufferStruct[] RunPlaneSideShader(Plane p, Vector3 pointOnPlane, Vector3[] ps) {
        
        PointBufferStruct[] points = new PointBufferStruct[ps.Length];
        PlaneBufferStruct[] plane = new PlaneBufferStruct[1];

        PointBufferStruct[] output = new PointBufferStruct[points.Length];

        for (int i = 0; i < points.Length; i++) {
            points[i] = new PointBufferStruct(ps[i]);
        }

        plane[0] = new PlaneBufferStruct(p.normal, pointOnPlane);

        ComputeBuffer planeBuffer = new ComputeBuffer(1, 24);
        ComputeBuffer pointBuffer = new ComputeBuffer(points.Length, 16);
        
        planeBuffer.SetData(plane);
        pointBuffer.SetData(points);
        
        int kernel = shader.FindKernel("PlaneSide");
        shader.SetBuffer(kernel, "planeBuffer", planeBuffer);
        shader.SetBuffer(kernel, "pointBuffer", pointBuffer);
        
        shader.Dispatch(kernel, points.Length, 1,1);

        pointBuffer.GetData(output);
        return output;
    }

    public void Slice(Plane plane, Vector3 pointOnPlane) {

        GameObject negative = GameObject.Instantiate(this.gameObject, this.transform.position, this.transform.rotation);
        GameObject positive = GameObject.Instantiate(this.gameObject, this.transform.position, this.transform.rotation);

        negative.transform.name = "negative";
        positive.transform.name = "positive";
        
        // reset left mesh
        MeshFilter leftMF = negative.GetComponent<MeshFilter>();
        leftMF.mesh = new Mesh();
        // reset right mesh
        MeshFilter rightMF = positive.GetComponent<MeshFilter>();
        rightMF.mesh = new Mesh();

        // give left and right new mesh slicers
        MeshSlicer leftSlicer = negative.GetComponent<MeshSlicer>();
        MeshSlicer rightSlicer = positive.GetComponent<MeshSlicer>();

        Vector3[] vertices = meshfilter.mesh.vertices;
        Vector3[] normals = meshfilter.mesh.normals;
        Vector2[] uv = meshfilter.mesh.uv;

        for (int i = 0; i < this.meshfilter.mesh.subMeshCount; i++) {
            int[] triangles = this.meshfilter.mesh.GetTriangles(i);
            
            Vector3[] test = new Vector3[triangles.Length];
            for (int j = 0; j < triangles.Length; j+=3) {
                test[j] = vertices[triangles[j]];
                test[j+1]= vertices[triangles[j+1]];
                test[j+2] = vertices[triangles[j+2]];
            }

            PointBufferStruct[] buffer = RunPlaneSideShader(plane, pointOnPlane, test);

            for (int j = 0; j < triangles.Length; j+=3) {
                int a = triangles[j];
                int b = triangles[j+1];
                int c = triangles[j+2];

                bool aPosSide = buffer[j].Side == 1;
                bool bPosSide = buffer[j+1].Side == 1;
                bool cPosSide = buffer[j+2].Side == 1;

                if (aPosSide && bPosSide && cPosSide) {
                    rightSlicer.AddTriangle(vertices[a], 
                                            vertices[b],
                                            vertices[c],
                                            normals[a],
                                            normals[b],
                                            normals[c],
                                            uv[a],
                                            uv[b],
                                            uv[c], i);

                } else if (!aPosSide && !bPosSide && !cPosSide) {
                    leftSlicer.AddTriangle(vertices[a],
                                            vertices[b],
                                            vertices[c],
                                            normals[a],
                                            normals[b],
                                            normals[c],
                                            uv[a],
                                            uv[b],
                                            uv[c], i);

                } else {
                    this.CutTriangle(a, aPosSide,
                                        b, bPosSide,
                                        c, cPosSide,
                                        pointOnPlane,
                                        plane,
                                        leftSlicer, rightSlicer, i);
                }
            }
        }

        // draw interior faces
        leftSlicer.DrawInteriorFaces(plane.normal);
        rightSlicer.DrawInteriorFaces(-plane.normal);

        leftSlicer.CommitTriangles();
        rightSlicer.CommitTriangles();

        // update collider mesh for collisions
        MeshCollider leftMeshCollider = negative.GetComponent<MeshCollider>();
        MeshCollider rightMeshCollider = positive.GetComponent<MeshCollider>();
        leftMeshCollider.sharedMesh = leftSlicer.meshfilter.mesh;
        rightMeshCollider.sharedMesh = rightSlicer.meshfilter.mesh;

        // destroy current game object
        Destroy(this.gameObject);
    }

    protected void DrawInteriorFaces(Vector3 normal) {
        if (this.interiorFacePoints == null) return;
        if (this.interiorFacePoints.Count < 3) return;

        if (interiorFaceTriangles == null) interiorFaceTriangles = new List<Triangle>();

        // going to put these triangles on a new submesh
        int submeshIndex = this.meshfilter.mesh.subMeshCount;
        this.meshfilter.mesh.subMeshCount++;
        
        // average out interior face center
        this.interiorFaceCenter /= this.interiorFacePoints.Count;

        float maxDistance = -1f;
        Vector3 furthestPoint = Vector3.zero;
        for (int i = 0; i < this.interiorFacePoints.Count; i++) {
            float curr = Vector3.Distance(this.interiorFaceCenter, this.interiorFacePoints[i]);            
            if (curr > maxDistance) {
                maxDistance = curr;
                furthestPoint = interiorFacePoints[i];
            }
        }

        Vector2 offset = new Vector2(0.5f, 0.5f);
        
        for (int i = 0; i < this.interiorFacePoints.Count; i+=2) {
            Vector2 uva = (this.interiorFaceCenter - this.interiorFacePoints[i])/maxDistance;
            Vector2 uvb = (this.interiorFaceCenter - this.interiorFacePoints[i+1])/maxDistance;

            Triangle t = new Triangle(this.interiorFaceCenter, this.interiorFacePoints[i], this.interiorFacePoints[i+1],
                                      normal, normal, normal,
                                      offset, uva+offset, uvb+offset);

            interiorFaceTriangles.Add(t); // this is for debugging purposes only
            AddTriangle(t, submeshIndex);
        }

        // add new material for inner surface
        Material[] mats = this.meshRenderer.materials;
        Array.Resize<Material>(ref mats, mats.Length+1);
        mats[mats.Length-1] = this.innerMaterial;
        this.meshRenderer.materials = mats;
    }

    protected void CutTriangle(int aIdx, bool aPlaneSide, int bIdx, bool bPlaneSide, int cIdx, bool cPlaneSide, Vector3 hitPoint, Plane p, MeshSlicer left, MeshSlicer right, int subMeshIndex) {
        Vector3[] vertices = meshfilter.mesh.vertices;
        Vector3[] normals = meshfilter.mesh.normals;
        Vector2[] uv = meshfilter.mesh.uv;

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
        Vector3 p0 = vertices[loneIdx];
        Vector3 p0normal = normals[loneIdx];
        Vector3 p0UV = uv[loneIdx];

        Vector3 p1 = vertices[p1Idx];
        Vector3 p1normal = normals[p1Idx];
        Vector3 p1UV = uv[p1Idx];

        Vector3 p2 = vertices[p2Idx];
        Vector3 p2normal = normals[p2Idx];
        Vector3 p2UV = uv[p2Idx];
        
        Vector3 p0MinusL0 = (hitPoint-p0);
        
        Vector3 line1 = (p1 - p0);
        Vector3 line2 = (p2 - p0);

        float p0MinusL0DotN = Vector3.Dot(p0MinusL0, p.normal);
        float d1 = p0MinusL0DotN / Vector3.Dot(line1, p.normal);
        float d2 = p0MinusL0DotN / Vector3.Dot(line2, p.normal);

        Vector3 i1 = p0 + line1*d1;
        Vector3 i2 = p0 + line2*d2;

        Vector3 i1Normal = Vector3.Lerp(p0normal, p1normal, d1);
        Vector2 i1UV = Vector2.Lerp(p0UV, p1UV, d1);

        Vector3 i2Normal = Vector3.Lerp(p0normal, p2normal, d2);
        Vector2 i2UV = Vector2.Lerp(p0UV, p2UV, d2);

        Triangle t1 = new Triangle(p0, i2, i1, p0normal, i2Normal, i1Normal, p0UV, i2UV, i1UV);
        Triangle t2 = new Triangle(p1, i1, i2, p1normal, i1Normal, i2Normal, p1UV, i1UV, i2UV);
        Triangle t3 = new Triangle(p1, i2, p2, p1normal, i2Normal, p2normal, p1UV, i2UV, p2UV);
                                    
        if (lonePlaneSide) {
            right.AddTriangle(t1, subMeshIndex);
            left.AddTriangle(t2, subMeshIndex);
            left.AddTriangle(t3, subMeshIndex);
        } else {
            left.AddTriangle(t1, subMeshIndex);
            right.AddTriangle(t2, subMeshIndex);
            right.AddTriangle(t3, subMeshIndex);
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

    void AddTriangle(Triangle t, int subMeshIndex) {
        this.AddTriangle(t.a, t.b, t.c, t.aNormal, t.bNormal, t.cNormal, t.uva, t.uvb, t.uvc, subMeshIndex);
    }

    void AddTriangle(   Vector3 a, Vector3 b, Vector3 c,
                        Vector3 aNormal, Vector3 bNormal, Vector3 cNormal,
                        Vector2 uva, Vector2 uvb, Vector2 uvc,
                        int subMeshIndex) { 
        
        Vector3 p0 = Vector3.zero;  Vector3 p1 = Vector3.zero;  Vector3 p2 = Vector3.zero;
        Vector3 n0 = Vector3.zero;  Vector3 n1 = Vector3.zero;  Vector3 n2 = Vector3.zero;
        Vector2 uv0 = Vector2.zero; Vector2 uv1 = Vector2.zero; Vector2 uv2 = Vector2.zero;

        if (Vector3.Dot(aNormal, Vector3.Cross(b-a, c-b)) > 0) {
            p0 = a;     n0 = aNormal;   uv0 = uva;
            p1 = b;     n1 = bNormal;   uv1 = uvb;
            p2 = c;     n2 = cNormal;   uv2 = uvc;
        } else {
            p0 = a;     n0 = aNormal;   uv0 = uva;
            p1 = c;     n1 = cNormal;   uv1 = uvc;
            p2 = b;     n2 = bNormal;   uv2 = uvb;
        }

        this.newVertices.Add(p0);  this.newVertices.Add(p1);  this.newVertices.Add(p2);
        this.newNormals.Add(n0);   this.newNormals.Add(n1);   this.newNormals.Add(n2);
        this.newUV.Add(uv0);      this.newUV.Add(uv1);      this.newUV.Add(uv2);

        if (!triangles.ContainsKey(subMeshIndex)) {
            this.triangles[subMeshIndex] = new List<int>();
        }

        List<int> subMeshTriangles = this.triangles[subMeshIndex];
        subMeshTriangles.Add(this.newVertices.Count-3);
        subMeshTriangles.Add(this.newVertices.Count-2);
        subMeshTriangles.Add(this.newVertices.Count-1);
    }

    void CommitTriangles () {
        meshfilter.mesh.vertices = newVertices.ToArray();
        meshfilter.mesh.normals = newNormals.ToArray();
        meshfilter.mesh.uv = newUV.ToArray();

        foreach(KeyValuePair<int, List<int>> entry in this.triangles) {
            this.meshfilter.mesh.SetTriangles(entry.Value.ToArray(), entry.Key);
        }

        // clear out
        this.newVertices = new List<Vector3>();
        this.newNormals = new List<Vector3>();
        this.triangles = new Dictionary<int, List<int>>();
        this.newUV = new List<Vector2>();
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
            Vector3 offset = this.transform.position;

        switch(this.debugOption) {
            case DebugOption.DRAW_INTERIOR_FACES:
                if (interiorFaceTriangles != null) {
                for (int i = 0;  i < interiorFaceTriangles.Count; i++) {
                    Triangle t = new Triangle(offset + interiorFaceTriangles[i].a, offset + interiorFaceTriangles[i].b, offset + interiorFaceTriangles[i].c);
                    DrawTriangle(t);
                }
            }
            break;
            case DebugOption.DRAW_VERTICES:
                for (int i = 0; i < this.meshfilter.mesh.vertexCount; i++) {
                    Gizmos.DrawCube(offset + this.meshfilter.mesh.vertices[i], new Vector3(.05f, .05f, .05f));
                }
                break;
            case DebugOption.DRAW_INTERIOR_FACE_VERTICES:
                if (this.interiorFacePoints == null) return;
                Gizmos.color = Color.white;
                for (int i = 0; i < this.interiorFacePoints.Count; i++) {
                    Gizmos.DrawCube(offset + this.interiorFacePoints[i], new Vector3(.05f, .05f, .05f));
                }

                break;
            case DebugOption.DRAW_NORMALS:
                for (int i = 0; i < this.meshfilter.mesh.vertexCount; i++) {
                    Gizmos.color = Color.blue;
                    Gizmos.DrawRay(offset + this.meshfilter.mesh.vertices[i], this.meshfilter.mesh.normals[i] * .3f);
                }
                break;
            case DebugOption.NONE:
            default:
                return;
        }
    }

}
