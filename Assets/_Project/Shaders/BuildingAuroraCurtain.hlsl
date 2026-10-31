#ifndef TOWER_BUILDING_AURORA_CURTAIN_INCLUDED
#define TOWER_BUILDING_AURORA_CURTAIN_INCLUDED

// Animated aurora curtain for the Aurora Arch (Tools/BuildingAero/build_aurora_arch.py).
//
// It follows how a real aurora curtain behaves:
//  * fine vertical rays (field-line striations) that drift along the curtain and flicker,
//  * slow folds in the curtain that brighten where the sheet bends back on itself,
//  * bright surges that travel along the curtain,
//  * a soft, swaying lower edge that fades in rather than a hard line,
//  * colours that blend with height (green base, teal, blue, violet) and drift along the curtain,
//    with pink tinges on the lower edge in places (user reference, 2026-10-31),
//  * calm and active spells over time (a slow substorm cycle), different on every floor.
//
// The horizontal coordinate is the angle around the tower, so one continuous curtain runs around a
// whole floor through every arch. All horizontal noise has a whole number of cells per revolution,
// so there is no seam where the angle wraps.

// Arch outline in object space (Unity X runs from the start pillar axis to the next one; Y is up).
#define AURORA_ARCH_SPAN 2.0944
#define AURORA_ARCH_RADIUS .7472   // half-width of the opening
#define AURORA_ARCH_SPRING .14     // the curve starts on the pillar tops
#define AURORA_ARCH_RISE .62       // low, round half-ellipse: crown at .76
#define AURORA_ARCH_TOP 2.15
#define AURORA_ARCH_RIM .10      // rounded inner rim radius: the front face starts this far from the opening

float AuroraHash(float i, float seed)
{
    float p=frac(i*.1031+seed*.3183);
    p*=p+33.33; p*=p+p;
    return frac(p);
}

// Value noise with `cells` cells per period.
float AuroraPeriodicNoise(float x, float cells, float seed)
{
    float i=floor(x), f=frac(x);
    float a=i-cells*floor(i/cells), b=a+1; b-=cells*floor(b/cells);
    f=f*f*(3-2*f);
    return lerp(AuroraHash(a,seed),AuroraHash(b,seed),f);
}

// One value per cell (no blending between neighbours), changing smoothly over time.
float AuroraCellValue(float x, float cells, float t, float seed)
{
    float i=floor(x); i-=cells*floor(i/cells);
    float ti=floor(t), tf=frac(t); tf=tf*tf*(3-2*tf);
    return lerp(AuroraHash(i,seed+ti*1.618),AuroraHash(i,seed+(ti+1)*1.618),tf);
}

// Like AuroraCellValue, but each cell eases into the next over its last quarter so ray edges stay soft.
float AuroraCellValueSoft(float x, float cells, float t, float seed)
{
    float f=smoothstep(.75,1,frac(x));
    return lerp(AuroraCellValue(x,cells,t,seed),AuroraCellValue(x+1,cells,t,seed),f);
}

// Periodic along x, free running in time.
float AuroraPeriodicNoise2(float x, float cells, float t, float seed)
{
    float ti=floor(t), tf=frac(t); tf=tf*tf*(3-2*tf);
    return lerp(AuroraPeriodicNoise(x,cells,seed+ti*1.618),AuroraPeriodicNoise(x,cells,seed+(ti+1)*1.618),tf);
}

float AuroraTimeNoise(float t, float seed)
{
    float i=floor(t), f=frac(t); f=f*f*(3-2*f);
    return lerp(AuroraHash(i,seed),AuroraHash(i+1,seed),f);
}

// Signed distance to the opening (a half-ellipse rising from the feet), negative inside the opening.
float AuroraOpeningDistance(float2 xy)
{
    float2 q=float2(abs(xy.x-AURORA_ARCH_SPAN*.5),xy.y-AURORA_ARCH_SPRING);
    if(q.y<0) return q.x-AURORA_ARCH_RADIUS;
    float2 ab=float2(AURORA_ARCH_RADIUS,AURORA_ARCH_RISE);
    float k0=length(q/ab), k1=length(q/(ab*ab));
    return k0*(k0-1)/max(k1,1e-5);                         // close approximation of the ellipse distance
}

struct AuroraRing
{
    float angle;        // 0..1 around the tower
    float circumference;
    float floorSeed;
};

AuroraRing AuroraRingAt(float3 positionWS, float3 originWS, float2 center, float floorHeight)
{
    AuroraRing ring;
    float floorIndex=floor((originWS.y+.5)/max(floorHeight,.01));
    ring.floorSeed=AuroraHash(floorIndex,7.31);
    float2 d=positionWS.xz-center;
    ring.angle=frac(atan2(d.y,d.x)/TWO_PI+ring.floorSeed);
    // Arches share one radius per floor; rounding keeps every arch on the same cell count.
    ring.circumference=TWO_PI*max(round(length(originWS.xz-center)*2)*.5,1);
    return ring;
}

// Cells per revolution for features of about `size` metres, and the coordinate in those cells.
float AuroraCells(AuroraRing ring, float size) { return max(1,round(ring.circumference/size)); }
float AuroraCoord(AuroraRing ring, float size) { return ring.angle*AuroraCells(ring,size); }

float AuroraFoldWarp(AuroraRing ring, float angle, float t, float foldAmount)
{
    ring.angle=angle;
    float a=AuroraPeriodicNoise(AuroraCoord(ring,7.)+t*.05,AuroraCells(ring,7.),3.1)-.5;
    float b=AuroraPeriodicNoise(AuroraCoord(ring,2.4)-t*.11,AuroraCells(ring,2.4),5.7)-.5;
    return foldAmount*(2.2*a+1.1*b);      // metres along the curtain
}

struct AuroraLight
{
    half3 color;
    half amount;    // 0..~3 brightness before strength
};

struct AuroraSettings
{
    float time;
    float base;         // mean lower-edge height
    float edgeWander;   // how far the lower edge meanders
    float rayDensity;   // ray count multiplier
    float rayDrift;     // m/s rays slide along the curtain
    float flicker;      // ray flicker rate
    float foldAmount;
    float surge;        // travelling brightening strength
    float activityMin, activityMax;
    float heightScale;  // vertical size of the curtain (1 = full upper wall)
    float coverage;     // share of the ring the curtain covers (the rest stays plain white)
    float footprint;    // metres covered by one pixel (ray anti-aliasing)
    half3 lowColor, midColor, highColor, fringeColor;
};

AuroraLight AuroraCurtain(AuroraRing ring, float height, AuroraSettings s)
{
    float t=s.time;
    // Activity cycle: calm spells and active spells, offset per floor.
    float activity=lerp(s.activityMin,s.activityMax,smoothstep(.2,.85,AuroraTimeNoise(t*.035+ring.floorSeed*41,1.7)));

    // Folds: displace the curtain along itself; where the sheet compresses it reads brighter.
    float delta=.03/ring.circumference;
    float warp=AuroraFoldWarp(ring,ring.angle,t,s.foldAmount);
    float slope=(AuroraFoldWarp(ring,ring.angle+delta,t,s.foldAmount)-AuroraFoldWarp(ring,ring.angle-delta,t,s.foldAmount))/.06;
    float fold=clamp(1/max(1+slope,.3),.6,2.6);
    float along=warp/ring.circumference;                 // in revolutions

    // Rays live in the curtain frame, slide along it and flicker; more active spells flicker faster.
    float drift=t*s.rayDrift/ring.circumference;
    float flick=t*s.flicker*(.6+.5*activity);
    float sizeA=.12/max(s.rayDensity,.05), sizeB=.045/max(s.rayDensity,.05);
    float cellsA=AuroraCells(ring,sizeA), cellsB=AuroraCells(ring,sizeB);
    float xa=(ring.angle+along+drift)*cellsA, xb=(ring.angle+along-drift*.6)*cellsB;
    float rayA=AuroraPeriodicNoise2(xa,cellsA,flick,11.3);
    float rayB=AuroraPeriodicNoise2(xb,cellsB,flick*1.7,19.9);
    // Far away the rays fall below a pixel: settle to their average instead of shimmering.
    float detailA=saturate(1.6-s.footprint*cellsA/ring.circumference*1.4);
    float detailB=saturate(1.6-s.footprint*cellsB/ring.circumference*1.4);
    float rays=.6*lerp(.18,pow(saturate(rayA),2.5),detailA)+.4*lerp(.12,pow(saturate(rayB),3),detailB);
    rays=saturate(rays*1.3);
    // Random length per ray: each ray gets its own top, from short stubs to tall streaks.
    float lenA=AuroraCellValueSoft(xa,cellsA,flick*.12,91.7), lenB=AuroraCellValueSoft(xb,cellsB,flick*.16,97.1);
    float reach=lerp(.08,1.25,pow(saturate(lenA),1.7));
    float reachB=lerp(.06,1.1,pow(saturate(lenB),1.7));

    // Lower edge: big sweeping waves, medium swells and small ripples, all travelling along the curtain,
    // so the base sways like a ribbon instead of a flat line.
    float wave=1.7*(AuroraPeriodicNoise(AuroraCoord(ring,2.6)+t*.07,AuroraCells(ring,2.6),29.1)-.5)
              +1.5*(AuroraPeriodicNoise(AuroraCoord(ring,1.1)-t*.13,AuroraCells(ring,1.1),31.7)-.5)
              +.35*(AuroraPeriodicNoise(AuroraCoord(ring,.42)+t*.27,AuroraCells(ring,.42),33.9)-.5);
    float edge=s.base+s.edgeWander*wave;
    float y=height-edge;
    float yS=y/max(s.heightScale,.05);                       // vertical sizes scale with _AuroraHeight

    // Surges: bright knots travelling along the curtain.
    float surge=pow(saturate(AuroraPeriodicNoise(AuroraCoord(ring,1.7)-t*.55,AuroraCells(ring,1.7),37.3)),3);
    float pulse=1+s.surge*(surge*2.2-.25);

    // Broad luminous sheets carry the finer rays, so the curtain reads as soft veils with streaks in them.
    float sheet=AuroraPeriodicNoise((ring.angle+along*.6)*AuroraCells(ring,.75)+t*.05,AuroraCells(ring,.75),41.3);
    sheet=.25+.75*smoothstep(.15,.85,sheet);
    float streak=rays;                                      // full-contrast rays for the hanging tips
    rays=lerp(rays,.4,.25);

    // No base line: every ray hangs below the curtain base by its own length (up to ~0.4 m), so the
    // bottom of the aurora is a ragged fringe of streaks ending at different heights, as in real aurora.
    // One length per ray, so tips end as soft streaks instead of V-shaped points between neighbours.
    float dropA=AuroraCellValueSoft(xa,cellsA,flick*.25,61.1);
    float dropB=AuroraCellValueSoft(xb,cellsB,flick*.35,67.3);
    float drop=.6*pow(saturate(dropA),2)*lerp(.35,1,detailA)+.25*pow(saturate(dropB),2)*detailB;
    float rayTip=smoothstep(-.3,.04,yS+drop);                 // each ray fades out gradually toward its own tip
    rayTip*=rayTip;
    float under=1-smoothstep(-.03,.14,yS);                    // the region around and below the base
    float bottom=smoothstep(-.04,.16,yS);                     // the veil body fades in slowly above it
    // Each ray fades out toward its own top; fine rays (B) can be taller or shorter than the broad ones.
    float top=lerp(reach,reachB,saturate(rayB*rayB*1.5));
    float tall=exp(-max(yS,0)/max(top*(.5+.7*rays),.05))*(1-smoothstep(top*.55,top*1.05,yS));
    float tails=under*rayTip*(.08+1.6*streak*streak);
    float halo=exp(min(yS,0)/.2)*.06;
    float amount=(bottom*tall*sheet*(.1+1.25*rays)+tails*sheet)*fold*pulse*activity+halo*activity*sheet;
    // Subtle, patchy curtain: whole stretches of the ring stay plain white, and each ray is broken into
    // segments with white gaps that drift slowly upward.
    float presence=.7*AuroraPeriodicNoise(AuroraCoord(ring,2.4)+t*.03,AuroraCells(ring,2.4),71.3)
                  +.3*AuroraPeriodicNoise(AuroraCoord(ring,.9)-t*.05,AuroraCells(ring,.9),73.9);
    // presence clusters around 0.5, so this threshold makes `coverage` roughly the lit share of the wall.
    float threshold=.5+(.5-s.coverage)*.36;
    float cover=smoothstep(threshold-.05,threshold+.07,presence);
    // Smooth blobs, stretched upward, drifting up the rays: soft white gaps, not blocks.
    float gapNoise=AuroraPeriodicNoise2(xa*1.5,max(1,round(cellsA*1.5)),height*2.4-t*.08,83.3);
    float breaks=lerp(.1,1,lerp(smoothstep(.28,.62,gapNoise),.7,1-detailA));
    amount*=cover*lerp(1,breaks,.85);
    // Soft highlight roll-off keeps the brightest folds coloured instead of clipping to white.
    amount=amount/(1+.55*amount);

    // Colours blend continuously: by height (green base, teal, blue, violet top) and along the curtain,
    // where slow drifting patches shift the whole column warmer or cooler and pink tinges the lower edge.
    float drift1=AuroraPeriodicNoise(AuroraCoord(ring,3.8)+t*.02,AuroraCells(ring,3.8),43.1)-.5;
    float drift2=AuroraPeriodicNoise(AuroraCoord(ring,1.4)-t*.035,AuroraCells(ring,1.4),47.9)-.5;
    float altitude=saturate(yS/.62+.55*drift1+.25*drift2);
    half3 color=lerp(s.lowColor,s.midColor,smoothstep(0,.4,altitude));
    color=lerp(color,s.highColor,smoothstep(.3,.75,altitude));
    color=lerp(color,s.fringeColor,smoothstep(.65,1,altitude)*.85);
    float blush=smoothstep(.12,.42,AuroraPeriodicNoise(AuroraCoord(ring,4.6)-t*.015,AuroraCells(ring,4.6),53.7)-.3);
    color=lerp(color,s.fringeColor,blush*(1-smoothstep(0,.35,saturate(yS/.62)))*.6);

    AuroraLight light;
    light.color=color;
    light.amount=amount;
    return light;
}

// Where on the arch the curtain may appear: front and back faces, clear of the opening and the top edge.
half AuroraMask(float2 xy)
{
    float d=AuroraOpeningDistance(xy);
    return smoothstep(AURORA_ARCH_RIM+.03,AURORA_ARCH_RIM+.11,d)*(1-smoothstep(AURORA_ARCH_TOP-.08,AURORA_ARCH_TOP-.025,xy.y));
}

#endif
