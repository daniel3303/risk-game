"""Generate original display geometry; the explicit game graph defines legal borders."""
import json
from pathlib import Path

continents = [
    ("north-america", "North America", 5, "#c39a59", [(-14,8),(-13,5),(-12,4),(-12,1),(-9,-1),(-7,-3),(-6,-2),(-7,0),(-5,3),(-5.5,6),(-8,9),(-10,10)]),
    ("south-america", "South America", 2, "#b98169", [(-7,-3),(-4,-3),(-2,-5),(-3,-8),(-5,-11),(-6,-10),(-7,-6)]),
    ("europe", "Europe", 5, "#739b8e", [(-1,7),(0,10),(3,9),(4,6),(3,3),(1,2),(-1.8,3),(-0.7,5.5)]),
    ("africa", "Africa", 3, "#c6a653", [(-3,2),(1,3),(4,1),(5,-2),(3,-7),(1,-9),(-1,-7),(-3,-2),(-4,0)]),
    ("asia", "Asia", 7, "#918fba", [(4,9),(8,11),(13,10),(16,8),(16,4),(14,3),(13,0),(11,-3),(9,-4),(8,-2),(7,0),(5,1),(4,3)]),
    ("australia", "Australia", 2, "#729db3", [(10,-7),(12,-6.7),(13,-7.3),(15.4,-6.8),(16.8,-8),(16,-10),(13,-10.5),(10,-9.8)]),
]
territories = [
    ("alaska","Alaska",0,-12,7), ("northwest-territory","Northwest Territory",0,-10,8),
    ("greenland","Greenland",0,-6.5,8), ("alberta","Alberta",0,-10,5),
    ("ontario","Ontario",0,-8,5), ("quebec","Quebec",0,-6,5),
    ("western-united-states","Western United States",0,-10,2),
    ("eastern-united-states","Eastern United States",0,-7,2), ("central-america","Central America",0,-7.5,-1),
    ("venezuela","Venezuela",1,-5.5,-4), ("peru","Peru",1,-6,-6),
    ("brazil","Brazil",1,-3.8,-6), ("argentina","Argentina",1,-5,-9),
    ("iceland","Iceland",2,-2,7.6), ("scandinavia","Scandinavia",2,0.5,8.2),
    ("russia","Russia",2,2.5,6.3), ("great-britain","Great Britain",2,-2,5.5),
    ("northern-europe","Northern Europe",2,0.2,5.3), ("western-europe","Western Europe",2,-1.5,3.3),
    ("southern-europe","Southern Europe",2,1.5,3.4),
    ("north-africa","North Africa",3,-1.8,0), ("egypt","Egypt",3,1.5,1),
    ("east-africa","East Africa",3,3,-2), ("congo","Congo",3,0,-3),
    ("south-africa","South Africa",3,1,-6.4), ("madagascar","Madagascar",3,4.6,-6.2),
    ("ural","Ural",4,5.3,7.5), ("siberia","Siberia",4,7.5,8.5),
    ("yakutsk","Yakutsk",4,11,9), ("kamchatka","Kamchatka",4,14.6,7.5),
    ("irkutsk","Irkutsk",4,10,6.5), ("mongolia","Mongolia",4,11,4.5),
    ("japan","Japan",4,16.5,3.4), ("afghanistan","Afghanistan",4,5.5,4.2),
    ("china","China",4,8.5,3), ("middle-east","Middle East",4,5.2,1.5),
    ("india","India",4,7.4,0), ("siam","Siam",4,10.5,-1.7),
    ("indonesia","Indonesia",5,10.4,-5.6), ("new-guinea","New Guinea",5,14.8,-5.5),
    ("western-australia","Western Australia",5,11.3,-8.5), ("eastern-australia","Eastern Australia",5,15,-8),
]
islands = {
    2: [(-7.4,8.5),(-7,10.8),(-5.4,10.2),(-5,8.5),(-6,7)],
    13: [(-3.5,8),(-3.1,8.7),(-1.7,8.5),(-1.5,7.8),(-2.6,7.2)],
    16: [(-3.3,6.5),(-2.3,6.9),(-1.5,5.2),(-2.3,4.5),(-3,5)],
    25: [(4.6,-4.8),(5.2,-5.2),(5.1,-6.5),(4.5,-7.3),(4,-6.3)],
    32: [(16.8,4.8),(17.2,4.1),(16.8,3),(16,2.5),(15.8,3.1)],
    38: [(8.7,-4.9),(9.5,-4.6),(11.2,-5.3),(12.4,-5.5),(12.2,-6.1),(10.5,-6),(9,-5.5)],
    39: [(13.6,-5.2),(15.3,-4.8),(16.1,-5.5),(15,-6.1),(13.7,-5.8)],
}
edges = [
    "0-1 0-3 0-29 1-2 1-3 1-4 2-4 2-5 2-13 3-4 3-6 4-5 4-6 4-7 5-7 6-7 6-8 7-8 8-9",
    "9-10 9-11 10-11 10-12 11-12 11-20",
    "13-14 13-16 14-15 14-16 14-17 15-17 15-19 15-26 15-33 15-35 16-17 16-18 17-18 17-19 18-19 18-20 19-20 19-21 19-35",
    "20-21 20-22 20-23 21-22 21-35 22-23 22-24 22-25 22-35 23-24 24-25",
    "26-27 26-33 26-34 27-28 27-30 27-31 27-34 28-29 28-30 29-30 29-31 29-32 30-31 31-32 31-34 33-34 33-35 33-36 34-36 34-37 35-36 36-37 37-38",
    "38-39 38-40 39-40 39-41 40-41",
]

def clip(poly, a, b, c):
    result = []
    for i, p in enumerate(poly):
        q = poly[(i+1) % len(poly)]
        dp, dq = a*p[0]+b*p[1]-c, a*q[0]+b*q[1]-c
        if dp <= 1e-8:
            result.append(p)
        if (dp < 0) != (dq < 0):
            t = dp/(dp-dq)
            result.append((p[0]+t*(q[0]-p[0]), p[1]+t*(q[1]-p[1])))
    return result

neighbors = [[] for _ in territories]
for group in edges:
    for edge in group.split():
        a,b = map(int, edge.split("-"))
        neighbors[a].append(b)
        neighbors[b].append(a)
data = {"name":"Classic World", "continents":[], "territories":[]}
for key,name,bonus,color,_ in continents:
    data["continents"].append({"id":key,"name":name,"bonus":bonus,"color":color})
for i,(key,name,continent,x,z) in enumerate(territories):
    shape = continents[continent][4]
    for j,(_,_,other,ox,oz) in enumerate(territories):
        if i == j or j in islands or other != continent:
            continue
        shape = clip(shape, 2*(ox-x), 2*(oz-z), ox*ox+oz*oz-x*x-z*z)
    if i in islands:
        shape = islands[i]
    shape = [[round(px*0.97+x*0.03,3),round(pz*0.97+z*0.03,3)] for px,pz in shape]
    data["territories"].append({"id":i,"key":key,"name":name,"continent":continents[continent][0],
        "x":x,"z":z,"neighbors":sorted(neighbors[i]),"shape":shape})
Path("content").mkdir(exist_ok=True)
Path("content/classic.json").write_text(json.dumps(data, indent=2)+"\n")
