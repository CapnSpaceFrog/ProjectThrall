using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum SelectionType
{
    Card,
    Unit,
    Hero,
    Row,
    Button,
    Board
}

public class Selectable : MonoBehaviour
{
	public delegate bool OnPrimary();
	public delegate void OnSecondary();

	public OnPrimary ReceivedPrimary;
	public OnSecondary ReceivedSecondary;

	public SelectionType Type;

   public object InstanceInfo;
}
