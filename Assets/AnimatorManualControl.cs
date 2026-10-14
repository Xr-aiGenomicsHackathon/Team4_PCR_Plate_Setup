using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class AnimatorManualControl : MonoBehaviour
{
    private Animator animator;
    private bool open = false;
    
    [SerializeField] private InputActionProperty openButton;
    void Start()
    {
        openButton.action.Enable();
        animator = GetComponent<Animator>();
    }

    private void Update()
    {
        if(openButton.action.triggered)
            ToggleState(); 
    }

    public void ToggleState()
    {
        open = !open;
        animator.SetBool("open", open);
    }
}
