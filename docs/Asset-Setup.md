# Environment dependency setup

ResearchStation references assets from **Creepy Cat / 3D Scifi Kit Starter Kit_HD**, under `Game/Assets/Creepy_Cat`. They were present in the existing local workspace but absent from the tracked dev checkout. The audit identified 41 referenced GUIDs belonging to this folder, including 20 prefab assets.

For this local recheck, the existing package and its `.meta` files were copied from the original workspace. This folder is ignored by Git. Its local restoration does not grant permission to redistribute the source package in the public repository.

Team members need their legitimately obtained matching package version. Install it at the same asset path and retain the original `.meta` files/GUIDs. Do not replace or regenerate GUIDs to hide missing references. Verify the station's prefab, mesh, material and shader references after import. No additional Asset Store purchase is needed for this machine.

Open `Game` in Unity **6000.3.25f1**, allow Package Manager and the first asset import to finish, and inspect Console before Play. The project includes `com.unity.pipeline` for Editor automation; it is tooling, not a gameplay dependency. No Library/Temp/cache folders should be committed.
