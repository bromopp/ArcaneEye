using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 10f;  // The speed of the spaceship movement
    public float rotationSpeed = 100f;  // The speed of the spaceship rotation
    public GameObject missilePrefab;  // The missile prefab that will be fired
    public Transform mainCamera;  // Reference to the main camera

    private Rigidbody spaceshipRigidbody;  // Reference to the spaceship's Rigidbody component

    private bool isFiring = false;  // Flag indicating whether the spaceship is currently firing a missile

    private void Start()
    {
        // Get the references to the spaceship's Rigidbody component and the main camera
        spaceshipRigidbody = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        // Get the input axes for the spaceship movement and rotation
        // Move the spaceship forward and backward using the W and S keys
        float moveInput = Input.GetAxis("Vertical");
        spaceshipRigidbody.AddRelativeForce(Vector3.forward * moveInput * moveSpeed);

    
        // Rotate the spaceship around the Y axis using the A and D keys
        float rotateInput = Input.GetAxis("Horizontal");
        spaceshipRigidbody.AddRelativeTorque(Vector3.up * rotateInput * rotationSpeed * Time.fixedDeltaTime);
        // If the player presses the fire button and the spaceship is not already firing a missile, fire a missile
        if (Input.GetKeyDown(KeyCode.Space) && !isFiring)
        {
            isFiring = true;
            ShootMissile();
        }
    }
    
    private void ShootMissile() {
     // Lock the spaceship controls during missile firing
            spaceshipRigidbody.constraints = RigidbodyConstraints.FreezeAll;

            // Switch to the missile camera view
            mainCamera.transform.SetParent(missilePrefab.transform);
            mainCamera.transform.localPosition = new Vector3(0f, 3f, -10f);
            mainCamera.transform.localRotation = Quaternion.Euler(20f, 0f, 0f);

            // Instantiate a new missile prefab and set its target
            GameObject missile = Instantiate(missilePrefab, transform.position + transform.forward * 2f, Quaternion.identity);
            missile.GetComponent<MissileController>().SetTarget(transform.forward);

            // Destroy the missile and switch back to the spaceship camera view after a delay
            Destroy(missile, 10f);
            Invoke("ResetControls", 3f);
    }
    private void ResetControls()
    {
        // Unlock the spaceship controls after missile firing
        spaceshipRigidbody.constraints = RigidbodyConstraints.None;

        // Switch back to the spaceship camera view
        mainCamera.transform.SetParent(transform);
        mainCamera.transform.localPosition = new Vector3(0f,30f,0f);
        mainCamera.transform.localRotation = Quaternion.Euler(90f, 90f, 0f);

        isFiring = false;
    }
}
