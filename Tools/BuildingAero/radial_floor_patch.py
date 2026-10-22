"""Canonical 4 m patch, with straight joins and angular subdivisions for ring bending."""
def radial_floor_patch(segments=6):
    vertices=[]
    profile=[(-.20,0),(-.15,0),(-.08,0),(-.012,0),(0,.004)]
    for y,inset in profile:
        for j in range(segments+1):vertices.append((inset,y,-inset-(4-2*inset)*j/segments))
        for j in range(segments,-1,-1):vertices.append((4-inset,y,-inset-(4-2*inset)*j/segments))
    n=2*(segments+1);faces=[]
    for row in range(len(profile)-1):
        for j in range(n):
            a=row*n+j;b=row*n+(j+1)%n;faces.append((a,b,b+n,a+n))
    for j in range(segments):
        faces.append((j,n-1-j,n-2-j,j+1))
        a=(len(profile)-1)*n;faces.append((a+j,a+j+1,a+n-2-j,a+n-1-j))
    return vertices,faces
