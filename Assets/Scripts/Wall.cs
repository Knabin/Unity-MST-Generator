using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

public class Wall : MonoBehaviour
{
[SerializeField]
    public List<GameObject> Meshes = new List<GameObject>();

    Random ran;

    void Start()
    {
        ran = new Random(UnityEngine.Random.Range(0, int.MaxValue));
        Meshes[ran.Next(0, Meshes.Count)].SetActive(true);
    }
}
