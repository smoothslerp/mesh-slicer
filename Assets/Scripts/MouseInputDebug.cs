using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MouseInputDebug : MonoBehaviour
{
    // Start is called before the first frame update
    List<Vector3> vertices;
    List<Vector3> direction;

    List<Vector3> meshVertices; 
    List<Vector3> meshNormals; 
    List<int> meshTriangles; 

    GameObject generated; 
    MeshFilter meshFilter;
    MeshSlicer slicerInstance;

    public int length;
    public Material material;


    // these 2 rays define the plane
    Ray start;
    Ray end;
    bool hitStarted = false;

    void Start()
    {

        // Physics.queriesHitBackfaces = true;
        this.vertices = new List<Vector3>();
        this.direction = new List<Vector3>();

        this.meshVertices = new List<Vector3>();
        this.meshNormals = new List<Vector3>();
        this.meshTriangles = new List<int>();

    }

    // Update is called once per frame
    void Update() {
        
        if (Input.GetKeyDown(KeyCode.M)) {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name, LoadSceneMode.Single);
        }

        // clear the lines 
        if (Input.GetMouseButtonDown(0)) {
            vertices.Clear();
            direction.Clear();

            meshVertices.Clear();
            meshNormals.Clear();
            meshTriangles.Clear();

            // allow the overwriting of start and end rays
            hitStarted = false; 

            // destroy old "slice"
            if (generated != null) GameObject.Destroy(generated);

            // create new "slice"
            Ray inputRay = Camera.main.ScreenPointToRay(Input.mousePosition);
            generated = new GameObject();
            
            meshFilter = generated.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = generated.AddComponent<MeshRenderer>();
            meshRenderer.material = this.material;
            
        }
        
        // draw lines
        if (Input.GetMouseButton(0)) {

            Ray inputRay = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit; 
            bool hitSomething = Physics.Raycast(inputRay.origin, inputRay.direction, out hit, length, LayerMask.NameToLayer("Defualt"), QueryTriggerInteraction.Ignore);

            if (!hitSomething && !hitStarted && this.vertices.Count == 0) { // TODO: the debugging vertices used to carry state is bad, fix thid
                start = inputRay;
                // temp(start);
            }
            
            if (!hitSomething && hitStarted) {
                end = inputRay;
                // temp(end);
                hitStarted = false;
                DrawPlane(direction[0] + vertices[0], Vector3.Cross(start.direction, end.direction).normalized);
                slicerInstance.Slice(new Plane(Vector3.Cross(start.direction, end.direction).normalized, direction[0] + vertices[0]), direction[0] + vertices[0]);
            }

            if (hitSomething) hitStarted = true;
            
            if (hitSomething) { // these are just for debugging purposes now
                vertices.Add(inputRay.origin);
                Vector3 hitVector = hit.point - inputRay.origin;
                direction.Add(hitVector);
                slicerInstance = hit.transform.GetComponent<MeshSlicer>();
            }
        }
    }

    void temp(Ray inputRay) {
        // for every vertex in vertices, there are 2 vertices in meshVertices
        meshVertices.Add(inputRay.origin); // near 
        meshVertices.Add(inputRay.origin + inputRay.direction * length); // far
        
        // update meshfilter
        meshFilter.mesh.vertices =  meshVertices.ToArray(); 
        
        // dont need to do rest of the routine for the first vertex and first 2 mesh vertices
        if (vertices.Count < 2) return; 

        int currentFarIdx = meshVertices.Count-1;
        int currentNearIdx = meshVertices.Count-2;
        // last 2 mesh vertices at: count-3(far),count-4(near)
        int prevFarIdx = meshVertices.Count-3; 
        int prevNearIdx = meshVertices.Count-4;

        // create 2 clockwise triangles with the last 2 meshvertices with the current 2 mesh vertices
        meshTriangles.Add(prevNearIdx);
        meshTriangles.Add(prevFarIdx);
        meshTriangles.Add(currentFarIdx);

        meshTriangles.Add(prevNearIdx);
        meshTriangles.Add(currentFarIdx);
        meshTriangles.Add(currentNearIdx);

        // update triangles
        meshFilter.mesh.triangles = meshTriangles.ToArray();
        
        Vector3 normal = Vector3.Cross(meshVertices[prevFarIdx] - meshVertices[prevNearIdx],
                                        meshVertices[currentFarIdx] - meshVertices[currentNearIdx]);
        normal.Normalize();
        meshNormals.Add(normal);
        meshNormals.Add(normal);
        meshNormals.Add(normal);
        meshNormals.Add(normal);

        // update normals
        // meshFilter.mesh.normals = meshNormals.ToArray();
        // TODO: TODO: also need uvs
    }

    private void OnDrawGizmos() {
        Gizmos.color = Color.red;
        Gizmos.DrawRay(start.origin, start.direction);
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(end.origin, end.direction);
    }

    private void DrawPlane(Vector3 position, Vector3 normal) {
 
        Vector3 v3;
        
        if (normal.normalized != Vector3.forward)
            v3 = Vector3.Cross(normal, Vector3.forward).normalized * normal.magnitude;
        else
            v3 = Vector3.Cross(normal, Vector3.up).normalized * normal.magnitude;;
            
        var corner0 = position + v3;
        var corner2 = position - v3;
        var q = Quaternion.AngleAxis(90.0f, normal);
        v3 = q * v3;
        var corner1 = position + v3;
        var corner3 = position - v3;
        
        Debug.DrawLine(corner0, corner2, Color.green, 3f);
        Debug.DrawLine(corner1, corner3, Color.green, 3f);
        Debug.DrawLine(corner0, corner1, Color.green, 3f);
        Debug.DrawLine(corner1, corner2, Color.green, 3f);
        Debug.DrawLine(corner2, corner3, Color.green, 3f);
        Debug.DrawLine(corner3, corner0, Color.green, 3f);
        Debug.DrawRay(position, normal, Color.red, 3f);
 }

}
