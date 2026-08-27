#!/usr/bin/env python3
import struct, lzma, json, re, sys
from pathlib import Path
import lz4.block

class R:
    def __init__(self,b,end='<',pos=0): self.b=b; self.e=end; self.p=pos
    def read(self,n):
        x=self.b[self.p:self.p+n]
        if len(x)!=n: raise EOFError((self.p,n,len(self.b)))
        self.p+=n; return x
    def u8(self): return self.read(1)[0]
    def i16(self): x=struct.unpack_from(self.e+'h',self.b,self.p)[0]; self.p+=2; return x
    def u16(self): x=struct.unpack_from(self.e+'H',self.b,self.p)[0]; self.p+=2; return x
    def i32(self): x=struct.unpack_from(self.e+'i',self.b,self.p)[0]; self.p+=4; return x
    def u32(self): x=struct.unpack_from(self.e+'I',self.b,self.p)[0]; self.p+=4; return x
    def i64(self): x=struct.unpack_from(self.e+'q',self.b,self.p)[0]; self.p+=8; return x
    def u64(self): x=struct.unpack_from(self.e+'Q',self.b,self.p)[0]; self.p+=8; return x
    def cstr(self):
        q=self.b.index(0,self.p); s=self.b[self.p:q].decode('utf-8','replace'); self.p=q+1; return s
    def align(self,n=4): self.p=(self.p+n-1)&~(n-1)
    def astr(self):
        n=self.i32()
        if n<0 or n>50_000_000: raise ValueError(('bad str len',n,self.p-4))
        s=self.read(n).decode('utf-8','replace'); self.align(4); return s

def be32(b,p): return struct.unpack_from('>I',b,p)[0]
def be64(b,p): return struct.unpack_from('>Q',b,p)[0]

def unityfs(path):
    b=Path(path).read_bytes(); r=R(b,'>')
    sig=r.cstr(); assert sig=='UnityFS',sig
    fmt=r.u32(); unity=r.cstr(); gen=r.cstr(); size=r.u64(); cinfo=r.u32(); uinfo=r.u32(); flags=r.u32()
    if flags & 0x80: info_pos=len(b)-cinfo
    else:
        if flags & 0x200: r.align(16)
        info_pos=r.p
    comp=b[info_pos:info_pos+cinfo]
    cm=flags & 0x3f
    if cm==0: info=comp
    elif cm in (2,3): info=lz4.block.decompress(comp,uncompressed_size=uinfo)
    elif cm==1: info=lzma.decompress(comp)
    else: raise ValueError(('info compression',cm))
    ir=R(info,'>'); ir.read(16)
    bc=ir.u32(); blocks=[]
    for _ in range(bc): blocks.append((ir.u32(),ir.u32(),ir.u16()))
    nc=ir.u32(); nodes=[]
    for _ in range(nc): nodes.append((ir.u64(),ir.u64(),ir.u32(),ir.cstr()))
    if flags & 0x80:
        data_pos=r.p
        if flags & 0x200: data_pos=(data_pos+15)&~15
    else:
        data_pos=info_pos+cinfo
        if flags & 0x200: data_pos=(data_pos+15)&~15
    out=bytearray(); p=data_pos
    for usize,csize,bf in blocks:
        c=b[p:p+csize]; p+=csize; m=bf&0x3f
        if m==0: d=c
        elif m in (2,3): d=lz4.block.decompress(c,uncompressed_size=usize)
        elif m==1: d=lzma.decompress(c)
        else: raise ValueError(('block compression',m))
        if len(d)!=usize: raise ValueError(('block size',len(d),usize))
        out+=d
    files={}
    for off,n,fl,name in nodes: files[name]=bytes(out[off:off+n])
    return files

class Serialized:
    def __init__(self,b):
        self.b=b
        ver=be32(b,8); self.version=ver
        endian=b[16]; self.e='<' if endian==0 else '>'
        if ver>=22:
            self.metadata_size=be32(b,20); self.file_size=be64(b,24); self.data_offset=be64(b,32); mp=48
        else:
            self.metadata_size=be32(b,0); self.file_size=be32(b,4); self.data_offset=be32(b,12); mp=20
        r=R(b,self.e,mp); self.unity=r.cstr(); self.platform=r.i32(); tt=bool(r.u8())
        type_count=r.i32(); self.types=[]
        for _ in range(type_count):
            classid=r.i32(); stripped=bool(r.u8()); scriptidx=r.i16()
            if classid==114: scriptid=r.read(16)
            oldhash=r.read(16)
            if tt:
                node_count=r.i32(); sbsize=r.i32()
                node_size=32 if ver>=19 else 24
                r.read(node_count*node_size); r.read(sbsize)
            if ver>=21:
                depc=r.i32()
                if depc<0 or depc>10000: raise ValueError(('bad dep count',depc,r.p,classid))
                r.read(depc*4)
            self.types.append(classid)
        obj_count=r.i32(); r.align(4); self.objects=[]
        if obj_count<0 or obj_count>1_000_000: raise ValueError(('bad object count',obj_count,r.p))
        for _ in range(obj_count):
            if ver>=14: r.align(4); pathid=r.i64()
            else: pathid=r.i32()
            bytestart=r.i64() if ver>=22 else r.u32()
            size=r.u32(); typeid=r.i32()
            classid=self.types[typeid] if 0<=typeid<len(self.types) else None
            if ver<16: _=r.i16()
            if ver<11: _=r.u16()
            if 11<=ver<17: _=r.i16()
            if ver in (15,16): _=r.u8()
            self.objects.append((pathid,self.data_offset+bytestart,size,typeid,classid))
    def object_bytes(self,o): return self.b[o[1]:o[1]+o[2]]

def mono_header(raw):
    r=R(raw,'<')
    r.i32(); r.i64(); r.u8(); r.align(4); r.i32(); r.i64(); name=r.astr()
    return name,r

def try_string_table(raw):
    try:
        name,r=mono_header(raw)
        locale=r.astr()
        if not re.fullmatch(r'[a-z]{2}(?:-[A-Z]{2})?', locale): return None
        r.i32(); r.i64()
        meta_count=r.i32()
        if not (0<=meta_count<=1000): return None
        if meta_count!=0: return None
        n=r.i32()
        if not (0<=n<=10000): return None
        entries=[]
        for _ in range(n):
            eid=r.i64(); value=r.astr(); mc=r.i32()
            if mc!=0: return None
            entries.append((eid,value))
        return {'name':name,'locale':locale,'entries':entries}
    except Exception:
        return None

def try_shared(raw):
    try:
        name,r=mono_header(raw)
        if not name.endswith(' Shared Data'): return None
        table=r.astr()
        guid=r.astr()
        if not re.fullmatch(r'[0-9a-fA-F]{32}', guid): return None
        n=r.i32()
        if not (0<=n<=10000): return None
        entries=[]
        for _ in range(n):
            eid=r.i64(); key=r.astr(); emc=r.i32()
            if emc!=0: return None
            entries.append((eid,key))
        return {'name':name,'table':table,'guid':guid,'entries':entries}
    except Exception:
        return None

def bundle_serialized_files(path):
    fs=unityfs(path); out=[]
    for name,b in fs.items():
        if b.startswith(b'UnityFS'): continue
        try: out.append((name,Serialized(b)))
        except Exception: pass
    return out

def main(root,outfile):
    aa=Path(root)/'How to Fish_Data/StreamingAssets/aa/StandaloneWindows64'
    shared=aa/'localization-assets-shared_assets_all.bundle'
    shared_tables={}
    for fn,sf in bundle_serialized_files(shared):
        for o in sf.objects:
            if o[4]!=114: continue
            x=try_shared(sf.object_bytes(o))
            if x and x['entries']:
                shared_tables[x['table']]={'guid':x['guid'],'keys':{str(i):k for i,k in x['entries']}}
    print('shared tables',len(shared_tables),sum(len(v['keys']) for v in shared_tables.values()))
    for k,v in sorted(shared_tables.items()): print(' ',k,len(v['keys']))

    bundles=sorted(aa.glob('localization-string-tables-*.bundle'))
    result={'version':1,'locales':[],'tables':{}}
    locale_seen=[]
    found=0
    for bp in bundles:
        tables=[]
        for fn,sf in bundle_serialized_files(bp):
            for o in sf.objects:
                if o[4]!=114: continue
                x=try_string_table(sf.object_bytes(o))
                if x and x['entries']: tables.append(x)
        if not tables:
            print('NO TABLES',bp.name); continue
        locale=max((x['locale'] for x in tables),key=lambda z:sum(1 for y in tables if y['locale']==z))
        if locale not in locale_seen: locale_seen.append(locale)
        for x in tables:
            loc=x['locale']
            table=x['name']
            suffix='_'+loc
            if table.endswith(suffix): table=table[:-len(suffix)]
            if table not in shared_tables:
                cand=[q for q in shared_tables if table.lower()==q.lower()]
                if cand: table=cand[0]
            keys=shared_tables.get(table,{}).get('keys',{})
            dest=result['tables'].setdefault(table,{'guid':shared_tables.get(table,{}).get('guid',''),'entries':{}})
            lmap={}
            for eid,val in x['entries']:
                key=keys.get(str(eid),str(eid)); lmap[key]={'id':eid,'value':val,'smart':('{' in val and '}' in val)}
            dest['entries'][loc]=lmap; found+=len(lmap)
        print(locale,bp.name,'tables',len(tables),'entries',sum(len(x['entries']) for x in tables))
    desired=['en','sv','zh-CN','zh-TW','fr','de','it','ja','ko','pl','pt-BR','ru','es-MX','es-ES','tr','uk']
    result['locales']=[x for x in desired if x in locale_seen]+[x for x in locale_seen if x not in desired]
    Path(outfile).write_text(json.dumps(result,ensure_ascii=False,separators=(',',':')),encoding='utf-8')
    print('OUT',outfile,Path(outfile).stat().st_size,'locales',len(result['locales']),'tables',len(result['tables']),'localized entries',found)
    missing=[]
    for t,d in result['tables'].items():
        for loc in result['locales']:
            if loc not in d['entries']: missing.append((t,loc,'table'))
            elif len(d['entries'][loc])!=len(shared_tables.get(t,{}).get('keys',{})): missing.append((t,loc,len(d['entries'][loc]),len(shared_tables.get(t,{}).get('keys',{}))))
    print('MISSING',len(missing)); print(missing[:30])

if __name__=='__main__': main(sys.argv[1],sys.argv[2])
