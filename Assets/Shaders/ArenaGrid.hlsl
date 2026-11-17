#ifndef DECOHERENCE_ARENA_GRID_INCLUDED
#define DECOHERENCE_ARENA_GRID_INCLUDED

// 到最近的 spacing 整数倍的距离，单位与传入值一致
float RepeatDistance(float value, float spacing)
{
    return abs(frac(value / spacing + 0.5) - 0.5) * spacing;
}

// 线宽随像素足迹收放：近处保持毫米级实宽，远处退到约一像素，避免摩尔纹。
// 用于"必须一直看得见"的实线（地面格线、围栏立杆与横撑）。
float LineMask(float offset, float halfWidth, float footprint)
{
    float width = max(halfWidth, footprint);

    return 1.0 - smoothstep(width - footprint, width + footprint, offset);
}

// 线宽落到亚像素时按能量衰减，让细网眼在远处淡成均匀的一层而不是糊成实心板。
// 用于围栏的网眼。
float LineCoverage(float offset, float halfWidth, float footprint)
{
    float width = max(halfWidth, footprint);
    float mask = 1.0 - smoothstep(width - footprint, width + footprint, offset);

    return mask * saturate(halfWidth / max(footprint, 1e-5));
}

#endif
