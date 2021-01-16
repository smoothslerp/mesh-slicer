using UnityEngine;
using UnityEngine.SceneManagement;

public class MouseInputDebug : MonoBehaviour {
    MeshSlicer slicerInstance;
    
    // the length of the raycast
    public int length;
    // 2 rays and a point to define a plane we will be cutting across
    Vector3 hitPoint;
    Ray start;
    Ray end;
    bool hitStarted = false;
    bool overwrite = true;

    void Update() {
        
        if (Input.GetKeyDown(KeyCode.M)) {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name, LoadSceneMode.Single);
        }

        if (Input.GetMouseButtonDown(0)) {
            // allow the overwriting of start and end rays (start again)
            overwrite = true;
            hitStarted = false; 
        }
        
        if (Input.GetMouseButton(0)) {

            RaycastHit hit; 
            Ray inputRay = Camera.main.ScreenPointToRay(Input.mousePosition);
            bool hitSomething = Physics.Raycast(inputRay.origin, inputRay.direction, out hit, length, ~LayerMask.NameToLayer("Default"), QueryTriggerInteraction.Ignore);

            // keep updating the start ray so that we have the latest start ray right 
            // before we start hitting something
            if (!hitSomething && !hitStarted && overwrite) start = inputRay;
            
            // we've hit something, so we record the point at which the object was hit
            if (hitSomething && !hitStarted) { 
                overwrite = false;
                hitStarted = true;
                hitPoint = hit.point;
                slicerInstance = hit.transform.GetComponent<MeshSlicer>();
            }

            // we record the first instance of the ray missing
            if (!hitSomething && hitStarted) {
                end = inputRay;
                hitStarted = false;
                DrawPlane(hitPoint, Vector3.Cross(start.direction, end.direction).normalized);
                
                Vector3 offset = -slicerInstance.transform.position;
                Quaternion q = Quaternion.Inverse(slicerInstance.transform.rotation);
                slicerInstance.Slice(new Plane(Vector3.Cross(q*start.direction, q*end.direction).normalized, q*(hitPoint+offset)), q*(hitPoint+offset));

            }
        }
    }

    private void OnDrawGizmos() {
        Gizmos.color = Color.red;
        Gizmos.DrawRay(start.origin, start.direction * length);
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(end.origin, end.direction * length);
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
