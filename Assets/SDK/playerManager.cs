using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace Playmanity
{
    public class playerManager : MonoBehaviour
    {
        public static string token;

        [System.Obsolete]
        public static bool validate(string token)
        {
            // NOT IMPLEMENTED.
            return true;
        }
    }
}