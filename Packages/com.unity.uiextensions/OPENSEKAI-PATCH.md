Upstream: https://github.com/Unity-UI-Extensions/com.unity.uiextensions
Version: 3.0.0
Commit: d47f838ba918bbb9b26f242bd944940a711f525f
License: BSD-3-Clause (LICENSE.md).

Embedded via UPM to preserve a reproducible compatibility patch. UIPrimitiveBase adds maxWidth/maxHeight = -1 for UGUI 2.6 ILayoutElement. FlowLayoutGroup and TableLayoutGroup pass an unset maximum size (-1) to the expanded UGUI 2.6 layout API. ColorPickerPresets uses Unity 6.6 GetEntityId. IsPrefab uses scene validity/loading and hide flags, removing the obsolete instance-ID sign heuristic. Other Runtime and Editor sources are unchanged. Samples are excluded and their package manifest entry removed. Do not edit Library/PackageCache.
