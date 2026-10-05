# Minimal chunk reader: yields (prefab, pos, data_bytes) for every ZDO in a world folder.
import struct, glob, os, sys
def numitems(b, p):
    n=b[p]; p+=1
    if n & 0x80: n=((n&0x7f)<<8)|b[p]; p+=1
    return n,p
def rstr(b,p):
    n=0; sh=0
    while True:
        c=b[p]; p+=1; n|=(c&0x7f)<<sh; sh+=7
        if not c&0x80: break
    return p+n
def scan(d):
    main=max(int(f.split('.')[1]) for f in os.listdir(d) if f.endswith('.chunks'))
    for f in glob.glob(os.path.join(d,'*.chunk')):
        b=open(f,'rb').read(); p=2; cnt=struct.unpack_from('<i',b,p)[0]; p+=4
        for _ in range(cnt):
            flags=struct.unpack_from('<H',b,p)[0]; p+=2
            if flags&0x2000: x,z=struct.unpack_from('<hh',b,p); y=0; p+=4
            else: x,y,z=struct.unpack_from('<3f',b,p); p+=12
            prefab=struct.unpack_from('<i',b,p)[0]; p+=4
            if flags&0x1000:
                v=struct.unpack_from('<H',b,p)[0]; p+=2
                if not v&0x8000: p+=2
            ds=p
            if flags&0xFF:
                if flags&1: p+=5
                for bit,sz in ((2,4),(4,12),(8,16),(0x10,4),(0x20,8)):
                    if flags&bit:
                        n,p=numitems(b,p); p+=n*(4+sz)
                if flags&0x40:
                    n,p=numitems(b,p)
                    for _ in range(n): p+=4; p=rstr(b,p)
                if flags&0x80:
                    n,p=numitems(b,p)
                    for _ in range(n): p+=4; l=struct.unpack_from('<i',b,p+0)[0]; p+=4+l
            yield prefab,(x,y,z),b[ds:p]
