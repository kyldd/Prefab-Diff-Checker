using System.Collections.Generic;

namespace PrefabDiffChecker
{
    public enum MatchReason
    {
        ExactHierarchy,
        SameParentAndName,
        SameName,
        Structural,
        Fuzzy
    }

    public class ObjectMatch
    {
        public ObjectSnapshot oldObject;
        public ObjectSnapshot newObject;

        public MatchReason reason;
        public float confidence;

        public bool WasRenamed
        {
            get
            {
                return oldObject != null &&
                       newObject != null &&
                       oldObject.name != newObject.name;
            }
        }

        public bool WasReordered
        {
            get
            {
                if (oldObject == null ||
                    newObject == null)
                {
                    return false;
                }

                return oldObject.siblingIndex !=
                       newObject.siblingIndex;
            }
        }
    }

    public class MatchResult
    {
        public readonly List<ObjectMatch> matches =
            new List<ObjectMatch>();

        public readonly List<ObjectSnapshot> unmatchedOld =
            new List<ObjectSnapshot>();

        public readonly List<ObjectSnapshot> unmatchedNew =
            new List<ObjectSnapshot>();

        public ObjectMatch FindByOld(
            ObjectSnapshot oldObject)
        {
            for (int i = 0; i < matches.Count; i++)
            {
                if (ReferenceEquals(
                        matches[i].oldObject,
                        oldObject))
                {
                    return matches[i];
                }
            }

            return null;
        }

        public ObjectMatch FindByNew(
            ObjectSnapshot newObject)
        {
            for (int i = 0; i < matches.Count; i++)
            {
                if (ReferenceEquals(
                        matches[i].newObject,
                        newObject))
                {
                    return matches[i];
                }
            }

            return null;
        }
    }
}
