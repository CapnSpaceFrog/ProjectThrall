using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum TouchableType
{
    Card,
    Unit,
    Hero,
    Row,
    Button,
    Board
}

public class Touchable : MonoBehaviour
{
    public TouchableType Type;

    public object InstanceInfo;
}
