using UnityEngine;
using System.Collections;

public class LookAtMouse : MonoBehaviour
{
    private Vector3 target;
    
	void Update () 
    {
        target = Camera.main.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, -Camera.main.transform.position.z));
        //target.Set(target.x, target.y, 0);

        Debug.DrawRay(target, Vector3.up);
        transform.LookAt(target);
	}
}
