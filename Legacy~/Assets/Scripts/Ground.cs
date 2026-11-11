using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Ground : MonoBehaviour
{
    void Start()
    {
        
    }

    void Update()
    {
        transform.position -= new Vector3(6 * Time.deltaTime, 0, 0);

        if(transform.position.x < -21) transform.position = new Vector3(28, transform.position.y, 0);
    }
}