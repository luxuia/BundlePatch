# BundlePatch
Unity AssetBundle Diff Patch

# 功能：
1. 拆分原始bundle，分析出被patch包覆盖的冗余资源并清理。
2. 打印被patch包覆盖的资源信息，可用于运行时patch生成
3. 打印bundle的完整信息，用于分析调试

# dump difference with a a_patch: 
BundlePatch.exe a a_patch --diff

# eraser patched part in a
BundlePatch.exe a a_patch --eraser

# dump asset info, like objs, pathids, resources
BundlePatch.exe a
