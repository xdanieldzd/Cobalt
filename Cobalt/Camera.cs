using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;

using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Input;

namespace Cobalt
{
    /* Modified/extended from http://neokabuto.blogspot.de/2014/01/opentk-tutorial-5-basic-camera.html */

    public class Camera
    {
        const float movementDivider = 25.0f;
        const float rotationDivider = 100.0f;

        float deltaTime;

        Vector3 position, orientation;

        public Vector3 Position { get { return position; } }
        public Vector3 Orientation { get { return orientation; } }

        public KeyConfig KeyConfiguration { get; set; }

        KeyboardState lastKbd;
        MouseState lastMouse;

        Vector2 mouseCenter, mousePosition;

        public Camera()
        {
            Reset();

            KeyConfiguration = new KeyConfig()
            {
                MoveForward = Key.W,
                MoveBackward = Key.S,
                StrafeLeft = Key.A,
                StrafeRight = Key.D
            };
        }

        public void Reset()
        {
            position = Vector3.Zero;
            orientation = new Vector3((float)Math.PI, 0.0f, 0.0f);
        }

        public Matrix4 GetViewMatrix()
        {
            Vector3 lookat = new Vector3();

            lookat.X = (float)(Math.Sin(orientation.X) * Math.Cos(orientation.Y));
            lookat.Y = (float)Math.Sin(orientation.Y);
            lookat.Z = (float)(Math.Cos(orientation.X) * Math.Cos(orientation.Y));

            return Matrix4.LookAt(position, position + lookat, Vector3.UnitY);
        }

        public void Update(float deltaTime)
        {
            this.deltaTime = deltaTime;

            /* Keyboard */
            KeyboardState kbdState = Keyboard.GetState();

            if (kbdState[KeyConfiguration.MoveForward]) this.Move(0.0f, 0.1f, 0.0f);
            if (kbdState[KeyConfiguration.MoveBackward]) this.Move(0.0f, -0.1f, 0.0f);
            if (kbdState[KeyConfiguration.StrafeLeft]) this.Move(-0.1f, 0.0f, 0.0f);
            if (kbdState[KeyConfiguration.StrafeRight]) this.Move(0.1f, 0.0f, 0.0f);

            lastKbd = kbdState;

            /* Mouse */
            MouseState mouseState = Mouse.GetState();

            if (mouseState.LeftButton == OpenTK.Input.ButtonState.Pressed)
            {
                if (lastMouse.LeftButton == OpenTK.Input.ButtonState.Released)
                    mouseCenter = new Vector2(mouseState.X, mouseState.Y);

                mousePosition = new Vector2(mouseState.X, mouseState.Y);
            }

            lastMouse = mouseState;

            Vector2 delta = mouseCenter - mousePosition;
            this.AddRotation(delta.X, delta.Y);

            mouseCenter = mousePosition;
        }

        private void Move(float x, float y, float z)
        {
            Vector3 offset = new Vector3();

            Vector3 forward = new Vector3((float)Math.Sin(orientation.X), (float)Math.Sin(orientation.Y), (float)Math.Cos(orientation.X));
            Vector3 right = new Vector3(-forward.Z, 0.0f, forward.X);

            offset += x * right;
            offset += y * forward;
            offset.Y += z;

            offset.NormalizeFast();
            offset = Vector3.Multiply(offset, deltaTime / movementDivider);

            position += offset;
        }

        private void AddRotation(double x, double y)
        {
            x = x / rotationDivider;
            y = y / rotationDivider;

            orientation.X = (float)((orientation.X + x) % ((float)Math.PI * 2.0f));
            orientation.Y = (float)Math.Max(Math.Min(orientation.Y + y, (float)Math.PI / 2.0f - 0.1f), -(float)Math.PI / 2.0f + 0.1f);
        }

        public class KeyConfig
        {
            public Key MoveForward { get; set; }
            public Key MoveBackward { get; set; }
            public Key StrafeLeft { get; set; }
            public Key StrafeRight { get; set; }

            public KeyConfig() { }
        }
    }
}
