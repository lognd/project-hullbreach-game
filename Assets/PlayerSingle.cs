using UnityEngine;

namespace Unity.Template.CompetitiveActionMultiplayer
{
    public class PlayerSingle : MonoBehaviour
    {
        public Rigidbody2D rb;
        public Transform thruster;
        public float speed;
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
        
        }

        // Update is called once per frame
        void Update()
        {
            if(Input.GetButtonDown("Jump")){
                print("testing");
                rb.AddForceAtPosition(transform.up * speed, thruster.position);
            }
        }
    }
}
