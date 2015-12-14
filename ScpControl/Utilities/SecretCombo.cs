using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScpControl.Utilities
{
    public class SecretCombo
    {
        private byte holdButton;
        private byte[] buttons;
        private int nextIndex = 0;
        private bool onlyHoldButtonHeld = false;

        public SecretCombo(byte holdButton, params byte[] buttonSequence)
        {
            this.holdButton = holdButton;
            this.buttons = buttonSequence;
        }

        public bool checkButtons(byte currentButtons)
        {
            if ((currentButtons & holdButton) == holdButton)
            {
                if (currentButtons == (holdButton | buttons[nextIndex]))
                {
                    nextIndex++;
                    onlyHoldButtonHeld = false;
                }
                else if (nextIndex > 0 && currentButtons == (holdButton | buttons[nextIndex - 1]))
                {
                    if (onlyHoldButtonHeld)
                    {
                        // User repressed the same button, reset
                        nextIndex = 0;
                    }
                    else
                    {
                        // Do nothing, still pressing the previous button without letting go
                    }
                }
                else if (currentButtons == holdButton)
                {
                    // Do nothing, just pressing the hold button
                    // Repressing previous is not valid anymore
                    onlyHoldButtonHeld = true;
                }
                else
                {
                    nextIndex = 0;
                }
            }
            else
            {
                nextIndex = 0;
            }

            if (nextIndex >= buttons.Length)
            {
                nextIndex = 0;
                return true;
            }
            return false;
        }
    }
}
