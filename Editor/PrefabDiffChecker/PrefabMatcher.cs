using System;
using System.Collections.Generic;

namespace PrefabDiffChecker
{
    public static class PrefabMatcher
    {
        private const float FuzzyThreshold = 0.78f;

        private const float AmbiguityMargin = 0.10f;

        private class Candidate
        {
            public ObjectSnapshot oldObject;
            public ObjectSnapshot newObject;

            public float score;
            public MatchReason reason;
        }

        public static MatchResult Match(
            PrefabSnapshot oldSnapshot,
            PrefabSnapshot newSnapshot)
        {
            MatchResult result =
                new MatchResult();

            List<ObjectSnapshot> oldObjects =
                new List<ObjectSnapshot>();

            List<ObjectSnapshot> newObjects =
                new List<ObjectSnapshot>();

            if (oldSnapshot != null &&
                oldSnapshot.root != null)
            {
                Flatten(
                    oldSnapshot.root,
                    oldObjects);
            }

            if (newSnapshot != null &&
                newSnapshot.root != null)
            {
                Flatten(
                    newSnapshot.root,
                    newObjects);
            }

            HashSet<ObjectSnapshot> matchedOld =
                new HashSet<ObjectSnapshot>();

            HashSet<ObjectSnapshot> matchedNew =
                new HashSet<ObjectSnapshot>();

            MatchExactHierarchy(
                oldObjects,
                newObjects,
                matchedOld,
                matchedNew,
                result);

            MatchSameParentAndName(
                oldObjects,
                newObjects,
                matchedOld,
                matchedNew,
                result);

            MatchUniqueNames(
                oldObjects,
                newObjects,
                matchedOld,
                matchedNew,
                result);

            MatchFuzzy(
                oldObjects,
                newObjects,
                matchedOld,
                matchedNew,
                result);

            for (int i = 0; i < oldObjects.Count; i++)
            {
                if (!matchedOld.Contains(oldObjects[i]))
                {
                    result.unmatchedOld.Add(
                        oldObjects[i]);
                }
            }

            for (int i = 0; i < newObjects.Count; i++)
            {
                if (!matchedNew.Contains(newObjects[i]))
                {
                    result.unmatchedNew.Add(
                        newObjects[i]);
                }
            }

            return result;
        }

        private static void MatchExactHierarchy(
            List<ObjectSnapshot> oldObjects,
            List<ObjectSnapshot> newObjects,
            HashSet<ObjectSnapshot> matchedOld,
            HashSet<ObjectSnapshot> matchedNew,
            MatchResult result)
        {
            Dictionary<string, ObjectSnapshot> newMap =
                new Dictionary<string, ObjectSnapshot>();

            for (int i = 0; i < newObjects.Count; i++)
            {
                ObjectSnapshot obj =
                    newObjects[i];

                if (!newMap.ContainsKey(
                        obj.hierarchyKey))
                {
                    newMap.Add(
                        obj.hierarchyKey,
                        obj);
                }
            }

            for (int i = 0; i < oldObjects.Count; i++)
            {
                ObjectSnapshot oldObject =
                    oldObjects[i];

                ObjectSnapshot newObject;

                if (!newMap.TryGetValue(
                        oldObject.hierarchyKey,
                        out newObject))
                {
                    continue;
                }

                if (matchedNew.Contains(newObject))
                    continue;

                AddMatch(
                    oldObject,
                    newObject,
                    MatchReason.ExactHierarchy,
                    1f,
                    matchedOld,
                    matchedNew,
                    result);
            }
        }

        private static void MatchSameParentAndName(
            List<ObjectSnapshot> oldObjects,
            List<ObjectSnapshot> newObjects,
            HashSet<ObjectSnapshot> matchedOld,
            HashSet<ObjectSnapshot> matchedNew,
            MatchResult result)
        {
            for (int i = 0; i < oldObjects.Count; i++)
            {
                ObjectSnapshot oldObject =
                    oldObjects[i];

                if (matchedOld.Contains(oldObject))
                    continue;

                string oldParent =
                    GetParentPath(
                        oldObject.hierarchyPath);

                ObjectSnapshot candidate =
                    null;

                int count = 0;

                for (int n = 0;
                     n < newObjects.Count;
                     n++)
                {
                    ObjectSnapshot newObject =
                        newObjects[n];

                    if (matchedNew.Contains(newObject))
                        continue;

                    if (!string.Equals(
                            oldObject.name,
                            newObject.name,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    string newParent =
                        GetParentPath(
                            newObject.hierarchyPath);

                    if (!string.Equals(
                            oldParent,
                            newParent,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    candidate =
                        newObject;

                    count++;
                }

                if (count == 1 &&
                    candidate != null)
                {
                    AddMatch(
                        oldObject,
                        candidate,
                        MatchReason.SameParentAndName,
                        0.98f,
                        matchedOld,
                        matchedNew,
                        result);
                }
            }
        }

        private static void MatchUniqueNames(
            List<ObjectSnapshot> oldObjects,
            List<ObjectSnapshot> newObjects,
            HashSet<ObjectSnapshot> matchedOld,
            HashSet<ObjectSnapshot> matchedNew,
            MatchResult result)
        {
            Dictionary<string, List<ObjectSnapshot>>
                oldNames =
                    BuildNameMap(
                        oldObjects,
                        matchedOld);

            Dictionary<string, List<ObjectSnapshot>>
                newNames =
                    BuildNameMap(
                        newObjects,
                        matchedNew);

            foreach (
                KeyValuePair<string, List<ObjectSnapshot>>
                    pair in oldNames)
            {
                List<ObjectSnapshot> newList;

                if (!newNames.TryGetValue(
                        pair.Key,
                        out newList))
                {
                    continue;
                }

                if (pair.Value.Count != 1 ||
                    newList.Count != 1)
                {
                    continue;
                }

                ObjectSnapshot oldObject =
                    pair.Value[0];

                ObjectSnapshot newObject =
                    newList[0];

                if (matchedOld.Contains(oldObject) ||
                    matchedNew.Contains(newObject))
                {
                    continue;
                }

                float structural =
                    CalculateStructuralSimilarity(
                        oldObject,
                        newObject);

                if (structural < 0.25f)
                    continue;

                float confidence =
                    0.88f +
                    structural * 0.10f;

                AddMatch(
                    oldObject,
                    newObject,
                    MatchReason.SameName,
                    confidence,
                    matchedOld,
                    matchedNew,
                    result);
            }
        }

        private static void MatchFuzzy(
            List<ObjectSnapshot> oldObjects,
            List<ObjectSnapshot> newObjects,
            HashSet<ObjectSnapshot> matchedOld,
            HashSet<ObjectSnapshot> matchedNew,
            MatchResult result)
        {
            List<Candidate> accepted =
                new List<Candidate>();

            for (int i = 0; i < oldObjects.Count; i++)
            {
                ObjectSnapshot oldObject =
                    oldObjects[i];

                if (matchedOld.Contains(oldObject))
                    continue;

                Candidate best =
                    null;

                Candidate secondBest =
                    null;

                for (int n = 0;
                     n < newObjects.Count;
                     n++)
                {
                    ObjectSnapshot newObject =
                        newObjects[n];

                    if (matchedNew.Contains(newObject))
                        continue;

                    float score =
                        CalculateMatchScore(
                            oldObject,
                            newObject);

                    Candidate candidate =
                        new Candidate();

                    candidate.oldObject =
                        oldObject;

                    candidate.newObject =
                        newObject;

                    candidate.score =
                        score;

                    candidate.reason =
                        MatchReason.Fuzzy;

                    if (best == null ||
                        candidate.score >
                        best.score)
                    {
                        secondBest =
                            best;

                        best =
                            candidate;
                    }
                    else if (
                        secondBest == null ||
                        candidate.score >
                        secondBest.score)
                    {
                        secondBest =
                            candidate;
                    }
                }

                if (best == null)
                    continue;

                if (best.score <
                    FuzzyThreshold)
                {
                    continue;
                }

                if (secondBest != null &&
                    best.score -
                    secondBest.score <
                    AmbiguityMargin)
                {
                    continue;
                }

                accepted.Add(best);
            }

            accepted.Sort(
                delegate(
                    Candidate a,
                    Candidate b)
                {
                    return b.score.CompareTo(
                        a.score);
                });

            for (int i = 0;
                 i < accepted.Count;
                 i++)
            {
                Candidate candidate =
                    accepted[i];

                if (matchedOld.Contains(
                        candidate.oldObject))
                {
                    continue;
                }

                if (matchedNew.Contains(
                        candidate.newObject))
                {
                    continue;
                }

                if (!IsMutualBestMatch(
                        candidate,
                        oldObjects,
                        matchedOld))
                {
                    continue;
                }

                AddMatch(
                    candidate.oldObject,
                    candidate.newObject,
                    candidate.reason,
                    candidate.score,
                    matchedOld,
                    matchedNew,
                    result);
            }
        }

        private static bool IsMutualBestMatch(
            Candidate candidate,
            List<ObjectSnapshot> oldObjects,
            HashSet<ObjectSnapshot> matchedOld)
        {
            float bestScore =
                -1f;

            ObjectSnapshot bestOld =
                null;

            float secondScore =
                -1f;

            for (int i = 0;
                 i < oldObjects.Count;
                 i++)
            {
                ObjectSnapshot oldObject =
                    oldObjects[i];

                if (matchedOld.Contains(oldObject))
                    continue;

                float score =
                    CalculateMatchScore(
                        oldObject,
                        candidate.newObject);

                if (score > bestScore)
                {
                    secondScore =
                        bestScore;

                    bestScore =
                        score;

                    bestOld =
                        oldObject;
                }
                else if (score > secondScore)
                {
                    secondScore =
                        score;
                }
            }

            if (!ReferenceEquals(
                    bestOld,
                    candidate.oldObject))
            {
                return false;
            }

            if (secondScore >= 0f &&
                bestScore -
                secondScore <
                AmbiguityMargin)
            {
                return false;
            }

            return true;
        }

        private static float CalculateMatchScore(
            ObjectSnapshot oldObject,
            ObjectSnapshot newObject)
        {
            float componentScore =
                CalculateComponentSimilarity(
                    oldObject,
                    newObject);

            float propertyScore =
                CalculatePropertySimilarity(
                    oldObject,
                    newObject);

            float childScore =
                CalculateChildSimilarity(
                    oldObject,
                    newObject);

            float nameScore =
                CalculateNameSimilarity(
                    oldObject.name,
                    newObject.name);

            float siblingScore =
                oldObject.siblingIndex ==
                newObject.siblingIndex
                    ? 1f
                    : 0f;

            float score =
                componentScore * 0.35f +
                propertyScore * 0.30f +
                childScore * 0.15f +
                nameScore * 0.15f +
                siblingScore * 0.05f;

            return Clamp01(score);
        }

        private static float CalculateStructuralSimilarity(
            ObjectSnapshot oldObject,
            ObjectSnapshot newObject)
        {
            float componentScore =
                CalculateComponentSimilarity(
                    oldObject,
                    newObject);

            float propertyScore =
                CalculatePropertySimilarity(
                    oldObject,
                    newObject);

            float childScore =
                CalculateChildSimilarity(
                    oldObject,
                    newObject);

            return Clamp01(
                componentScore * 0.45f +
                propertyScore * 0.40f +
                childScore * 0.15f);
        }

        private static float CalculateComponentSimilarity(
            ObjectSnapshot oldObject,
            ObjectSnapshot newObject)
        {
            HashSet<string> oldTypes =
                BuildComponentTypeSet(
                    oldObject);

            HashSet<string> newTypes =
                BuildComponentTypeSet(
                    newObject);

            return Jaccard(
                oldTypes,
                newTypes);
        }

        private static HashSet<string> BuildComponentTypeSet(
            ObjectSnapshot obj)
        {
            HashSet<string> set =
                new HashSet<string>();

            for (int i = 0;
                 i < obj.components.Count;
                 i++)
            {
                ComponentSnapshot component =
                    obj.components[i];

                string key =
                    component.typeName +
                    "#" +
                    component.typeIndex;

                set.Add(key);
            }

            return set;
        }

        private static float CalculatePropertySimilarity(
            ObjectSnapshot oldObject,
            ObjectSnapshot newObject)
        {
            Dictionary<string, string> oldProperties =
                BuildPropertySignatureMap(
                    oldObject);

            Dictionary<string, string> newProperties =
                BuildPropertySignatureMap(
                    newObject);

            if (oldProperties.Count == 0 &&
                newProperties.Count == 0)
            {
                return 1f;
            }

            HashSet<string> allKeys =
                new HashSet<string>();

            foreach (
                string key in oldProperties.Keys)
            {
                allKeys.Add(key);
            }

            foreach (
                string key in newProperties.Keys)
            {
                allKeys.Add(key);
            }

            if (allKeys.Count == 0)
                return 1f;

            float total =
                0f;

            foreach (string key in allKeys)
            {
                string oldValue;
                string newValue;

                bool hasOld =
                    oldProperties.TryGetValue(
                        key,
                        out oldValue);

                bool hasNew =
                    newProperties.TryGetValue(
                        key,
                        out newValue);

                if (!hasOld ||
                    !hasNew)
                {
                    continue;
                }

                total += 0.45f;

                if (string.Equals(
                        oldValue,
                        newValue,
                        StringComparison.Ordinal))
                {
                    total += 0.55f;
                }
            }

            return Clamp01(
                total /
                allKeys.Count);
        }

        private static Dictionary<string, string>
            BuildPropertySignatureMap(
                ObjectSnapshot obj)
        {
            Dictionary<string, string> result =
                new Dictionary<string, string>();

            for (int i = 0;
                 i < obj.components.Count;
                 i++)
            {
                ComponentSnapshot component =
                    obj.components[i];

                string componentKey =
                    component.typeName +
                    "#" +
                    component.typeIndex;

                for (int p = 0;
                     p < component.properties.Count;
                     p++)
                {
                    PropertySnapshot property =
                        component.properties[p];

                    string key =
                        componentKey +
                        "/" +
                        property.path;

                    result[key] =
                        property.value ?? "";
                }
            }

            return result;
        }

        private static float CalculateChildSimilarity(
            ObjectSnapshot oldObject,
            ObjectSnapshot newObject)
        {
            HashSet<string> oldChildren =
                new HashSet<string>();

            HashSet<string> newChildren =
                new HashSet<string>();

            for (int i = 0;
                 i < oldObject.children.Count;
                 i++)
            {
                oldChildren.Add(
                    oldObject.children[i].name);
            }

            for (int i = 0;
                 i < newObject.children.Count;
                 i++)
            {
                newChildren.Add(
                    newObject.children[i].name);
            }

            return Jaccard(
                oldChildren,
                newChildren);
        }

        private static float CalculateNameSimilarity(
            string a,
            string b)
        {
            if (string.Equals(
                    a,
                    b,
                    StringComparison.Ordinal))
            {
                return 1f;
            }

            if (string.IsNullOrEmpty(a) ||
                string.IsNullOrEmpty(b))
            {
                return 0f;
            }

            string lowerA =
                a.ToLowerInvariant();

            string lowerB =
                b.ToLowerInvariant();

            if (lowerA == lowerB)
                return 0.98f;

            int distance =
                LevenshteinDistance(
                    lowerA,
                    lowerB);

            int maxLength =
                Math.Max(
                    lowerA.Length,
                    lowerB.Length);

            if (maxLength == 0)
                return 1f;

            return Clamp01(
                1f -
                (float)distance /
                maxLength);
        }

        private static int LevenshteinDistance(
            string a,
            string b)
        {
            int[,] matrix =
                new int[
                    a.Length + 1,
                    b.Length + 1];

            for (int i = 0;
                 i <= a.Length;
                 i++)
            {
                matrix[i, 0] = i;
            }

            for (int j = 0;
                 j <= b.Length;
                 j++)
            {
                matrix[0, j] = j;
            }

            for (int i = 1;
                 i <= a.Length;
                 i++)
            {
                for (int j = 1;
                     j <= b.Length;
                     j++)
                {
                    int cost =
                        a[i - 1] ==
                        b[j - 1]
                            ? 0
                            : 1;

                    matrix[i, j] =
                        Math.Min(
                            Math.Min(
                                matrix[i - 1, j] + 1,
                                matrix[i, j - 1] + 1),
                            matrix[i - 1, j - 1] +
                            cost);
                }
            }

            return matrix[
                a.Length,
                b.Length];
        }

        private static void AddMatch(
            ObjectSnapshot oldObject,
            ObjectSnapshot newObject,
            MatchReason reason,
            float confidence,
            HashSet<ObjectSnapshot> matchedOld,
            HashSet<ObjectSnapshot> matchedNew,
            MatchResult result)
        {
            if (oldObject == null ||
                newObject == null)
            {
                return;
            }

            if (matchedOld.Contains(oldObject) ||
                matchedNew.Contains(newObject))
            {
                return;
            }

            ObjectMatch match =
                new ObjectMatch();

            match.oldObject =
                oldObject;

            match.newObject =
                newObject;

            match.reason =
                reason;

            match.confidence =
                Clamp01(confidence);

            result.matches.Add(match);

            matchedOld.Add(oldObject);
            matchedNew.Add(newObject);
        }

        private static void Flatten(
            ObjectSnapshot obj,
            List<ObjectSnapshot> result)
        {
            if (obj == null)
                return;

            result.Add(obj);

            for (int i = 0;
                 i < obj.children.Count;
                 i++)
            {
                Flatten(
                    obj.children[i],
                    result);
            }
        }

        private static Dictionary<string, List<ObjectSnapshot>>
            BuildNameMap(
                List<ObjectSnapshot> objects,
                HashSet<ObjectSnapshot> matched)
        {
            Dictionary<string, List<ObjectSnapshot>>
                result =
                    new Dictionary<string, List<ObjectSnapshot>>();

            for (int i = 0;
                 i < objects.Count;
                 i++)
            {
                ObjectSnapshot obj =
                    objects[i];

                if (matched.Contains(obj))
                    continue;

                List<ObjectSnapshot> list;

                if (!result.TryGetValue(
                        obj.name,
                        out list))
                {
                    list =
                        new List<ObjectSnapshot>();

                    result.Add(
                        obj.name,
                        list);
                }

                list.Add(obj);
            }

            return result;
        }

        private static string GetParentPath(
            string hierarchyPath)
        {
            if (string.IsNullOrEmpty(
                    hierarchyPath))
            {
                return "";
            }

            int index =
                hierarchyPath.LastIndexOf('/');

            if (index < 0)
                return "";

            return hierarchyPath.Substring(
                0,
                index);
        }

        private static float Jaccard(
            HashSet<string> a,
            HashSet<string> b)
        {
            if (a.Count == 0 &&
                b.Count == 0)
            {
                return 1f;
            }

            int intersection =
                0;

            foreach (string value in a)
            {
                if (b.Contains(value))
                    intersection++;
            }

            int union =
                a.Count +
                b.Count -
                intersection;

            if (union <= 0)
                return 1f;

            return
                (float)intersection /
                union;
        }

        private static float Clamp01(
            float value)
        {
            if (value < 0f)
                return 0f;

            if (value > 1f)
                return 1f;

            return value;
        }
    }
}
