using UnityEngine;
using UnityEngine.SceneManagement;

public class MouseInputDebug : MonoBehaviour {
    MeshSlicer slicerInstance;
    public int length;

    Vector3 hitPoint;
    // these 2 rays define the plane
    Ray start;
    Ray end;
    bool hitStarted = false;

    void Update() {
        
        if (Input.GetKeyDown(KeyCode.M)) {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name, LoadSceneMode.Single);
        }

        if (Input.GetMouseButtonDown(0)) {
            // allow the overwriting of start and end rays (start again)
            hitStarted = false; 
        }
        
        if (Input.GetMouseButton(0)) {

            RaycastHit hit; 
            Ray inputRay = Camera.main.ScreenPointToRay(Input.mousePosition);
            bool hitSomething = Physics.Raycast(inputRay.origin, inputRay.direction, out hit, length, LayerMask.NameToLayer("Defualt"), QueryTriggerInteraction.Ignore);

            // keep updating the start ray so that we have the latest start ray right 
            // before we start hitting something
            if (!hitSomething && !hitStarted) start = inputRay;
            
            // we've hit something, so we record the point at which the object was hit
            if (hitSomething && !hitStarted) { 
                hitStarted = true;
                hitPoint = hit.point;
                slicerInstance = hit.transform.GetComponent<MeshSlicer>();
            }

            // we record the first instance of the ray missing
            if (!hitSomething && hitStarted) {
                end = inputRay;
                hitStarted = false;
                DrawPlane(hitPoint, Vector3.Cross(start.direction, end.direction).normalized);
                slicerInstance.Slice(new Plane(Vector3.Cross(start.direction, end.direction).normalized, hitPoint), hitPoint);
            }
        }
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
