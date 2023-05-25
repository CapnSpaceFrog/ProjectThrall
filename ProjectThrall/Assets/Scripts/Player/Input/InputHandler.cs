using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputHandler : MonoBehaviour
{
    InputControls inputControls;

    static InputControls.MovementActions MovementControls;
    static InputControls.MouseActions MouseControls;
    static InputControls.UIActions UIControls;

    #region Mouse Input
    public static Vector2 MouseDelta;
    public static Vector2 MousePosition;
	#endregion

	#region Movement Input
	public static Vector3 MovementVector;
    #endregion

    #region Events
    //public static event Action<InteractInput> OnInteractInput;
    //public static event Action OnMenuInput;
    public static event Action OnLeftMousePress;
	 public static event Action OnLeftMouseCancel;
	 public static event Action OnRightMousePress;
	#endregion

	public void Awake()
    {
        inputControls = new InputControls();

        MovementControls = inputControls.Movement;
        MouseControls = inputControls.Mouse;
        UIControls = inputControls.UI;
    }

    public void OnEnable()
    {
        MovementControls.Enable();
        MouseControls.Enable();
        UIControls.Enable();

        //Movement Inputs
        MovementControls.Movement.performed += CacheMovementVector;

        //Mouse Inputs
        MouseControls.MouseDelta.performed += CacheMouseDelta;
        MouseControls.MousePos.performed += CacheMousePos;

        MouseControls.LeftClick.performed += LeftMousePressed;
		MouseControls.LeftClick.canceled += LeftMouseCancel;
		MouseControls.RightClick.performed += RightMousePressed;

        //UI Inputs
        UIControls.Pause.performed += GamePaused;

        //UI.Menu.performed += CacheMenuInput;
    }

	#region Mouse Event Functions
	private void CacheMouseDelta(InputAction.CallbackContext ctx)
    {
        MouseDelta = ctx.ReadValue<Vector2>();
    }

	private void CacheMousePos(InputAction.CallbackContext ctx)
	{
		MousePosition = ctx.ReadValue<Vector2>();
	}

	private void LeftMousePressed(InputAction.CallbackContext ctx)
    {
		OnLeftMousePress?.Invoke();
	}

	private void LeftMouseCancel(InputAction.CallbackContext ctx)
	{
		OnLeftMouseCancel?.Invoke();
	}

	private void RightMousePressed(InputAction.CallbackContext ctx)
	{
        OnRightMousePress?.Invoke();
	}

	#endregion

	private void CacheMovementVector(InputAction.CallbackContext ctx)
    {
        MovementVector = new Vector3(ctx.ReadValue<Vector2>().x, 0, ctx.ReadValue<Vector2>().y);
    }

    private void GamePaused(InputAction.CallbackContext ctx)
    {

    }
}
