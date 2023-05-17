using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;

public class MissileController : MonoBehaviour
{
    private Vector3 targetPosition;  // The position of the target that the missile is heading towards
    public Rigidbody missileBody;
    public CinemachineVirtualCamera cameraTarget;
    public float rotationSpeed = 100f;  // The speed of the spaceship rotation
    public float force = 5f;
    public float maxSpeed = 50f;
    public void SetTarget(Vector3 target)
    {

    }
    private void Start(){
        cameraTarget.enabled = true;
        //missile.GetComponent<MissileController>().SetTarget(transform.forward);

    }
    private void Update()
    {
        // Rotate the spaceship around the Y axis using the A and D keys
        if(missileBody.velocity.magnitude <= maxSpeed){
            missileBody.AddRelativeForce(Vector3.forward*force);
        }
        float rotateInput = Input.GetAxis("Horizontal");
        missileBody.AddRelativeTorque(Vector3.up * rotateInput * rotationSpeed * Time.fixedDeltaTime);
        //Debug.Log("x: "+ missileBody.position.x +",y: "+ missileBody.position.y +",z: " + missileBody.position.z);
        Debug.Log("missil velocity: " +missileBody.velocity.magnitude);
    }
}