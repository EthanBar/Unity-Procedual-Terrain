using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpawnerTHing : MonoBehaviour {

    public GameObject gm;

	// Use this for initialization
	void Start () {
		    
	}
	
	// Update is called once per frame
	void Update () {
        for (int i = 0; i < 1; i++) {
            GameObject thing = Instantiate(gm);
            //thing.transform.localScale = new Vector3(20, 20, 20);
        }

	}
}
