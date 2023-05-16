using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MissileController : MonoBehaviour
{
    private Vector3 targetPosition;  // The position of the target that the missile is heading towards
    public Transform cameraTarget;
    public void SetTarget(Vector3 target)
    {

    }

    private void FixedUpdate()
    {
        // Move the missile towards the target position
        Vector3 direction = targetPosition - transform.position;
        GetComponent<Rigidbody>().velocity = direction.normalized * 50f;

        cameraTarget.position = transform.position;
        // Rotate the missile to face the direction of movement
        transform.rotation = Quaternion.LookRotation(GetComponent<Rigidbody>().velocity);
    }
}