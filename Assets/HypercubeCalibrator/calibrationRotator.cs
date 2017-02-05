using UnityEngine;
using System.Collections;

public class calibrationRotator : MonoBehaviour {

	public float xSpeed = .01f;
	public float ySpeed = .01f;
	public float zSpeed = .01f;

    public bool paused = false;

	// Update is called once per frame
	void Update () 
	{
        if (Input.GetKeyDown(KeyCode.P))
            paused = !paused;

        if (paused)
            return;

		transform.Rotate (xSpeed * Time.deltaTime, ySpeed * Time.deltaTime, zSpeed * Time.deltaTime);
	}
}
