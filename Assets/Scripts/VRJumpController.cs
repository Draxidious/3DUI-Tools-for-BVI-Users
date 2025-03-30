using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(XROrigin))]
public class VRJumpController : MonoBehaviour
{
    public float jumpHeight = 1.5f;
    public float gravity = -9.81f;
    public float moveSpeed = 1.5f;

    private CharacterController characterController;
    private XROrigin xrOrigin;

    private float verticalVelocity = 0.0f;
    private bool isGrounded;

    private InputAction jumpInput;
    private InputAction moveInput;

    void Start()
    {
        characterController = GetComponent<CharacterController>();
        xrOrigin = GetComponent<XROrigin>();

        // Bind buttons directly
        jumpInput = new InputAction(type: InputActionType.Button, binding: "<XRController>{RightHand}/primaryButton");
        jumpInput.Enable();

        moveInput = new InputAction(type: InputActionType.Value, binding: "<XRController>{LeftHand}/thumbstick");
        moveInput.Enable();
    }

    void Update()
    {
        isGrounded = characterController.isGrounded;

        // Gravity reset
        if (isGrounded && verticalVelocity < 0)
            verticalVelocity = 0f;

        // Jumping
        if (isGrounded && jumpInput.WasPressedThisFrame())
            verticalVelocity += Mathf.Sqrt(jumpHeight * -2f * gravity);

        // Apply gravity
        verticalVelocity += gravity * Time.deltaTime;

        // Movement input
        Vector2 input = moveInput.ReadValue<Vector2>();
        Vector3 move = new Vector3(input.x, 0, input.y);

        // Rotate relative to head direction
        var head = xrOrigin.Camera.transform;
        move = head.TransformDirection(move);
        move.y = 0;
        move.Normalize();

        // Combine horizontal and vertical movement
        Vector3 finalMove = move * moveSpeed + Vector3.up * verticalVelocity;
        characterController.Move(finalMove * Time.deltaTime);
    }

    private void OnDestroy()
    {
        jumpInput.Disable();
        moveInput.Disable();
    }
}
