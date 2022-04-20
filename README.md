# BundlePatch
Unity AssetBundle Diff Patch

# 功能：
1. 拆分原始bundle，分析出被patch包覆盖的冗余资源并清理。
2. 打印被patch包覆盖的资源信息，可用于运行时patch生成
3. 打印bundle的完整信息，用于分析调试
4. 重组bundle包，加快加载性能，方便二进制更新
5. 二进制更新，基于重组的bundze包做资源级别的diff patch

# dump difference with a a_patch: 
BundlePatch.exe a a_patch --diff

# clean patched part in bundle a
BundlePatch.exe a a_patch --clean

# dump asset info, like objs, pathids, resources
BundlePatch.exe a

# reconstruct bundle for optimize load speed
BundlePatch.exe a --base

# make patch from base bundle
BundlePatch.exe base patch --patch