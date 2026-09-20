using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GetBounds : MonoBehaviour
{
    [SerializeField] private BoxCollider boxCollider;   
    // Start is called before the first frame update
    void Start()
    {
        Debug.Log(boxCollider.bounds.size);
        Debug.Log(boxCollider.bounds.center);
        Debug.Log(boxCollider.bounds);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
