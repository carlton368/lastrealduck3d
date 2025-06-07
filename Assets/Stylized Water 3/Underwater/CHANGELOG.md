3.1.1

Added:
- Underwater Area now supports rotated box colliders

Changed:
- Detection of the active Underwater Area now also factors which is closest, to better handle two or more overlapping areas

Fixed:
- Lake volume in demo not having the same material assigned as the water mesh
- Removed missing prefabs from demo scene (remnant of showcasing materials)
- Waterline Lens Offset parameter also appearing to clip the underwater fog near the camera

3.1.0

Added:
- Particle Effect controller, makes effects follow the camera underwater up to a specific depth (eg. sunshafts)
- Support for mobile hardware.

Changed:
- Rewritten rendering for Unity's Render Graph
- Optimized technical design, no longer uses full-screen post-processing.
- Redesigned to work exclusively with defined underwater areas (box collider triggers)
- Revised shader for transparency, now only blend the alpha value.

Removed:
- Blur/distortion effects, incurred maintenance overhead as URP's rendering code kept changing. Considered niche as AAA games do not use distortion effects underwater.
- Volume-based settings blending functionality (deemed unused).