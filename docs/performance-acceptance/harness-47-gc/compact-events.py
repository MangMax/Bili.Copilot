import pathlib,json,sys
root=pathlib.Path(sys.argv[1])
names={'GC/Start','GC/Stop','GC/SuspendEEStart','GC/SuspendEEStop','GC/RestartEEStart','GC/RestartEEStop','GC/Triggered'}
for p in root.glob('*/events.json'):
    data=json.loads(p.read_text(encoding='utf-8-sig'))
    data['selection']='All GC start/stop/suspend/restart/trigger events; other GC event kinds remain in hashed nettrace.'
    data['events']=[e for e in data['events'] if e['name'] in names]
    p.write_text(json.dumps(data,separators=(',',':')))
