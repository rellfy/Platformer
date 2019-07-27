using UnityEngine;
using System.Collections;
using CharacterControllers;


public class CameraFollow : MonoBehaviour {
    public Transform target;
    public float smoothDampTime = 0.2f;
    [HideInInspector]
    public new Transform transform;
    public Vector3 cameraOffset;
    public bool useFixedUpdate = false;

    private Platformer platformer;
    private Vector3 smoothDampVelocity;

    void Awake() {
        transform = gameObject.transform;
        this.platformer = target.GetComponent<Platformer>();
    }

    void LateUpdate() {
        if (!this.useFixedUpdate)
            updateCameraPosition();
    }

    void FixedUpdate() {
        if (this.useFixedUpdate)
            updateCameraPosition();
    }

    void updateCameraPosition() {
        if (this.platformer == null) {
            transform.position = Vector3.SmoothDamp(transform.position, target.position - this.cameraOffset, ref this.smoothDampVelocity, this.smoothDampTime);
            return;
        }

        if (this.platformer.velocity.x > 0) {
            transform.position = Vector3.SmoothDamp(transform.position, target.position - this.cameraOffset, ref this.smoothDampVelocity, this.smoothDampTime);
        } else {
            var leftOffset = this.cameraOffset;
            leftOffset.x *= -1;
            transform.position = Vector3.SmoothDamp(transform.position, target.position - leftOffset, ref this.smoothDampVelocity, this.smoothDampTime);
        }
    }

}