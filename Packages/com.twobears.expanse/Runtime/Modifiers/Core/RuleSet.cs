using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TwoBears.Expanse
{
    [CreateAssetMenu(menuName = "Expanse/Rule Set")]
    public class RuleSet : ScriptableObject
    {
        public Modifier[] modifiers;

#if UNITY_EDITOR
        public void Add(Modifier modifier)
        {
            if (modifiers == null) modifiers = new Modifier[] { modifier };
            else
            {
                Modifier[] old = modifiers;
                modifiers = new Modifier[old.Length + 1];

                for (int i = 0; i < old.Length; i++)
                {
                    modifiers[i] = old[i];
                }
                modifiers[old.Length] = modifier;
            }
        }
        public void RemoveAt(int index)
        {
            if (modifiers.Length > index)
            {
                Modifier[] old = modifiers;
                modifiers = new Modifier[old.Length - 1];

                for (int i = 0; i < modifiers.Length; i++)
                {
                    if (i < index) modifiers[i] = old[i];
                    else modifiers[i] = old[i + 1];
                }
            }
        }
        public void InsertAt(int index, Modifier modifier)
        {

        }
#endif
    }
}