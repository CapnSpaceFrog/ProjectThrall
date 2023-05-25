using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Moveable : Selectable
{
	public delegate void OnHeld(Vector3 mouseWorldPos);
	public delegate void OnRelease();

	public OnHeld WhileSelected;
	public OnRelease Released;
}