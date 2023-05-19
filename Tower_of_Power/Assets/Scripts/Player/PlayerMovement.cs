using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{   
    [Header("Movement Variables")]
    public CharacterController controller;
    public float PlayerSpeed = 3f;
    private Vector3 velocity;
    private float gravity = -9.81f;

    // Update is called once per frame
    void FixedUpdate()
    {
        Vector3 move = (transform.right * Input.GetAxis("Horizontal")) + (transform.forward * Input.GetAxis("Vertical")) + (transform.up * Input.GetAxis("Jump"));

        velocity.y += gravity * Time.deltaTime;
        controller.Move(move * PlayerSpeed * Time.fixedDeltaTime);
        controller.Move(velocity * Time.fixedDeltaTime);
    }
}
