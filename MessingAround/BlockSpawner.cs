using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BlockSpawner : MonoBehaviour {

    public GameObject gm;
    public int size;

	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
        for (int i = 0; i < size; i++) {
            for (int x = 0; x < size; x++) {
                for (int r = 0; r < size; r++) {
                    GameObject block = Instantiate(gm);
                    block.transform.position = new Vector3(i, x, r);
                }
            }
        }
	}
}
