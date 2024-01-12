using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AdBannerRotation : MonoBehaviour
{
    public float Speed = 15;

    private void Update()
    {
        transform.Rotate(Vector3.up, Speed * Time.deltaTime);
    }
}
