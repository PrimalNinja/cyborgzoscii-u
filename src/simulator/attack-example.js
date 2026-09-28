function attack(api){
  for(const s of api.structures) api.log(s.name+': '+s.addrs.length+' addresses');
  return 'example finished';
}