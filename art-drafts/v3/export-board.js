// Assembles review boards from unchanged native camera captures. This is a comparison artifact, not a browser screenshot.
window.makeComparisonBoard=async function(view){
await show(view);await document.fonts.ready;
const c=document.createElement('canvas');c.width=1600;c.height=700;const g=c.getContext('2d');
function text(s,x,y,size=14,color='#eaf7fa',weight=500){g.fillStyle=color;g.font=weight+' '+size+'px system-ui,sans-serif';g.fillText(s,x,y)}
g.fillStyle='#081d2c';g.fillRect(0,0,c.width,c.height);
text('DEEP FEAST / IMPLEMENTED UNITY ENVIRONMENT',24,26,11,'#83e1cd',700);
text(views[view].title,24,63,30,'#eaf7fa',700);text(views[view].description,24,89,14,'#abc1cd');
for(let i=0;i<2;i++){const x=24+i*784;g.fillStyle='#153245';g.fillRect(x,108,768,465);
text(i?'AFTER · IMPLEMENTED NATIVE UNITY PLAYER':'BEFORE · COMMITTED UNITY da45f4b',x+12,130,12,i?'#83e1cd':'#b8cbd5',700);
g.drawImage(document.getElementById(i?'after':'before'),x,141,768,432);}
views[view].notes.forEach(([title,note],i)=>{const x=24+i*522;text(title,x,603,16,'#eaf7fa',700);const words=note.split(' ');let line='',row=0;
for(const word of words){const next=line?line+' '+word:word;if(g.measureText(next).width>494){text(line,x,627+row++*20,13,'#abc1cd');line=word}else line=next}
if(line)text(line,x,627+row*20,13,'#abc1cd')});
text('Real Unity renders · Same camera, terrain and fish placement · Native offscreen capture · Windowed play requires an active display',24,688,11,'#88a6b7');
return await new Promise(resolve=>c.toBlob(resolve,'image/png'));
};
