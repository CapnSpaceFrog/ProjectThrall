using System.Collections;
using System.Collections.Generic;
using UnityEngine;
 using UnityEngine.SceneManagement;

public class MouseLook : MonoBehaviour
{
    [Range(0.01f, 10f)]
    public float mouseSensitivity;

    public Transform playerBody;

    float xRotation = 0f;

    public PlayerMovement Player;
    public GameObject[] Menus;
    public GameObject Crosshair;
    public Transition Transition;

    // Start is called before the first frame update
    void Start()
    {
        mouseSensitivity *= 150;
    }

    // Update is called once per frame
    void Update()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        playerBody.Rotate(Vector3.up * mouseX);

        foreach(GameObject g in Menus)
        {
            if(g.activeSelf)
            {
                Crosshair.SetActive(false);
                Cursor.lockState = CursorLockMode.None;
                Player.PlayerSpeed = 0f;
                if(Input.GetKeyDown(KeyCode.X))
                {
                    StartCoroutine(Transition.Reload(Menus));
                    Cursor.lockState = CursorLockMode.Locked;
                    Player.GetComponent<PlayerMovement>().PlayerSpeed = 3f;
                }
                break;
            }else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Crosshair.SetActive(true);
                Player.GetComponent<PlayerMovement>().PlayerSpeed = 3f;
            }
        }
    }
}
