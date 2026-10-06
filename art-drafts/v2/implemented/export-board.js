// Review-board assembly from untouched native captures; this is not a screenshot or generated concept.
window.makeComparisonBoard = async function(view) {
  await show(view);
  await document.fonts.ready;
  const board = document.createElement('canvas'); board.width = 1280; board.height = 800;
  const g = board.getContext('2d'), mint = '#75e6d1', muted = '#a1bac8';
  const text = (s,x,y,size=12,color='#f2fafb',weight=500) => {
    g.fillStyle=color; g.font=`${weight} ${size}px "Avenir Next",sans-serif`; g.fillText(s,x,y);
  };
  const panel = (x,y,w,h) => {g.fillStyle='#0d293a';g.strokeStyle='#28485b';g.lineWidth=1;g.beginPath();g.roundRect(x,y,w,h,10);g.fill();g.stroke();};
  g.fillStyle='#071d2d'; g.fillRect(0,0,1280,800);
  text('DEEP FEAST / IMPLEMENTED UNITY POLISH',26,29,10,mint,800);
  text(document.getElementById('title').textContent,26,66,27,'#f2fafb',700);
  text(document.getElementById('description').textContent,26,91,12,muted);
  const cards=[...document.querySelectorAll('.card')];
  if(view==='remaining'||view==='original'){
    const tall=view==='original',cols=tall?3:2,w=tall?400:606,h=tall?226:191,gap=tall?14:16;
    cards.forEach((card,i)=>{
      const x=26+(i%cols)*(w+gap),y=112+Math.floor(i/cols)*(h+12);
      panel(x,y,w,h);text(card.querySelector('h2').textContent,x+12,y+22,14,'#f2fafb',700);
      const canvases=[...card.querySelectorAll('canvas')];
      canvases.forEach((c,j)=>{
        const pane=x+j*w/2,iw=tall?180:245,ih=tall?220*180/245:130;
        text(j?'AFTER · IMPLEMENTED':'BEFORE · 5f75e02',pane+12,y+44,9,j?mint:muted,800);
        g.drawImage(c,pane+(w/2-iw)/2,y+50,iw,ih);
      });
    });
  }else if(view==='bites'){
    cards.forEach((card,i)=>{
      const x=26,y=112+i*201,w=1228;panel(x,y,w,189);
      text(card.querySelector('h2').textContent,x+12,y+22,14,'#f2fafb',700);
      [...card.querySelectorAll('.pane')].forEach((pane,j)=>{
        const px=x+j*w/2; text(j?'AFTER · IMPLEMENTED':'BEFORE · 5f75e02',px+12,y+44,9,j?mint:muted,800);
        [...pane.querySelectorAll('canvas')].forEach((c,k)=>{
          const cx=px+k*w/4+(w/4-c.width)/2;g.drawImage(c,cx,y+48);
          text(k?'OPEN JAW':'RELAXED',cx+40,y+180,9,muted,700);
        });
      });
    });
  }else{
    const pictures=[...document.querySelectorAll('.scene img')];
    for(let i=0;i<pictures.length;i++){
      const x=26+i*622;panel(x,112,606,380);
      text(i?'AFTER · IMPLEMENTED UNITY PLAYER':'BEFORE · COMMITTED UNITY 5f75e02',x+12,137,10,i?mint:muted,800);
      g.drawImage(pictures[i],x,151,606,606*720/1280);
    }
    [...document.querySelectorAll('.notes > div')].forEach((note,i)=>{
      const x=26+i*415; panel(x,514,400,114);text(note.querySelector('b').textContent,x+12,540,13,'#f2fafb',700);
      const words=note.querySelector('p').textContent.split(' ');let line='',row=0;
      for(const word of words){const next=line?line+' '+word:word;if(next.length>56){text(line,x+12,564+row++*17,11,muted);line=word;}else line=next;}
      if(line)text(line,x+12,564+row*17,11,muted);
    });
  }
  text('Both sides are actual Unity game renders. After: native macOS camera + HUD captured offscreen.',26,771,10,muted);
  text('Gameplay spawns differ. Windowed play still needs an active display. See HANDOVER.md for validation.',26,788,10,muted);
  return await new Promise(resolve=>board.toBlob(resolve,'image/png'));
};
