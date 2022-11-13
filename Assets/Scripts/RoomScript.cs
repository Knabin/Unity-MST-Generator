using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RoomScript : MonoBehaviour
{
    public enum WallType
    {
        LEFT,
        TOP,
        RIGHT,
        BOTTOM,
    };

    [SerializeField]
    GameObject[] quads = new GameObject[4];

    public void Test(WallType type)
    {
        Debug.Log((int)type);
        quads[(int)type].SetActive(false);
    }
}
