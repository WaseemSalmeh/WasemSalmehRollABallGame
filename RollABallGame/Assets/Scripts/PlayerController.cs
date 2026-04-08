using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class PlayerController : MonoBehaviour
{
    public float speed = 0;
    public float jumpForce = 7f;
    public TextMeshProUGUI countText;
    public GameObject winTextObject;

    private Rigidbody rb;
    private int count;
    private float movementX;
    private float movementY;
    private bool isGrounded = true;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        ResetRun();
    }

    void OnMove(InputValue movementValue)
    {
        Vector2 movementVector = movementValue.Get<Vector2>();

        movementX = movementVector.x;
        movementY = movementVector.y;
    }

    void SetCountText()
    {
        if (countText != null)
        {
            countText.text = "Count: " + count;
        }

        if (winTextObject != null)
        {
            winTextObject.SetActive(count >= 11);
        }
    }

    public void ResetRun()
    {
        count = 0;
        movementX = 0f;
        movementY = 0f;
        isGrounded = true;
        SetCountText();
    }

    void OnJump(InputValue jumpValue)
    {
        if (jumpValue.isPressed && isGrounded)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            isGrounded = false;
        }
    }

    void FixedUpdate()
    {
        Vector3 movement = new Vector3(movementX, 0.0f, movementY);

        rb.AddForce(movement * speed);
    }

    private void OnCollisionEnter(Collision collision)
    {
        isGrounded = true;

        if (collision.gameObject.CompareTag("Enemy"))
        {
            // Destroy the current object
            Destroy(gameObject);
            // Update the winText to display "You Lose!"
            winTextObject.gameObject.SetActive(true);
            winTextObject.GetComponent<TextMeshProUGUI>().text = "You Lose!";
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("PickUp"))
        {
            other.gameObject.SetActive(false);
            count++;

            SetCountText();
        }
    }
}
