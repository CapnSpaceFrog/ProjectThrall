using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Updateable : Damageable
{
	public delegate bool OnTouch();
	public delegate void TouchUpdate(Vector3 mouseWorldPos);
	public delegate void OnRelease();
	public delegate void OnCancel();

	public OnTouch Touched;
	public TouchUpdate WhileTouched;
	public OnRelease Released;
	public OnCancel InputCancelled;
}
