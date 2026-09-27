(() => {
  const docs = [document];
  for (const frame of document.querySelectorAll('iframe')) {
    try { if (frame.contentDocument) docs.push(frame.contentDocument); } catch (_) {}
  }
  const doc = docs.find(d => d.body && d.body.innerText.includes('查看更多日程表')) || document;
  const text = doc.body.innerText;
  const start = text.indexOf('日程表');
  const end = text.indexOf('查看更多日程表', start);
  if (start < 0 || end < 0) return JSON.stringify({ready:false,items:[]});
  const schedule = text.slice(start, end);
  const links = [...doc.querySelectorAll('a[href*="/calendar/launch/attempt/"]')];
  const items = [];
  let cursor = 0;
  for (let i=0; i<links.length; i++) {
    const a = links[i];
    const title = a.innerText.trim();
    const at = schedule.indexOf(title, cursor);
    if (!title || at < 0) continue;
    const nextTitle = links[i+1]?.innerText.trim();
    const next = nextTitle ? schedule.indexOf(nextTitle, at + title.length) : schedule.length;
    const tail = schedule.slice(at + title.length, next < 0 ? schedule.length : next);
    const before = schedule.slice(0, at);
    const groups = ['过期','今天截止','本周截止','以后截止'];
    let group = groups.reduce((best,g) => before.lastIndexOf(g) > before.lastIndexOf(best) ? g : best, '过期');
    const due = tail.match(/逾期\s*\d+\s*天|截止\s*\d+月\s*\d+|今天截止|Due[^\n]+/);
    if (!due) return JSON.stringify({ready:false,items:[]});
    const course = tail.slice(due.index + due[0].length).split(/\n/).map(s=>s.trim()).filter(s=>s && !groups.includes(s))[0];
    if (!course) return JSON.stringify({ready:false,items:[]});
    const url = new URL(a.href);
    const id = url.pathname.split('/').pop();
    if (url.host !== 'bb.sustech.edu.cn' || !/^_blackboard\.platform\.gradebook2\.GradableItem-_[0-9]+_1$/.test(id)) return JSON.stringify({ready:false,items:[]});
    const graded = text.slice(text.indexOf('最近评分'));
    const pos = graded.indexOf(title);
    const note = pos >= 0 && graded.slice(pos,pos+title.length+course.length+40).includes(course) ? '最近评分中有同名记录，请核对' : null;
    items.push({id,title,course,due:due[0].replace(/\s+/g,' ').trim(),group,note});
    cursor = at + title.length;
  }
  // A failed or partially loaded page must never erase previously known work.
  const explicitEmpty = /没有.*(作业|事件|截止)|无.*(作业|事件)|No (items|events|due dates)/i.test(schedule);
  return JSON.stringify({ready:items.length === links.length && (items.length > 0 || explicitEmpty),items});
})();
