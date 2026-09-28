(() => {
  const documents = [document];
  for (const frame of document.querySelectorAll('iframe')) {
    try { if (frame.contentDocument) documents.push(frame.contentDocument); } catch (_) {}
  }
  const doc = documents.find(d => d.body && /查看更多日程表|View all.*calendar/i.test(d.body.innerText)) || document;
  const text = doc.body?.innerText || '';
  const endMatch = /查看更多日程表|View all.*calendar/i.exec(text);
  const startMatch = /日程表|Calendar/i.exec(text);
  const fail = () => JSON.stringify({ready: false, items: []});
  if (!startMatch || !endMatch || endMatch.index <= startMatch.index) return fail();
  const schedule = text.slice(startMatch.index, endMatch.index);
  const links = [...doc.querySelectorAll('a[href*="/calendar/launch/attempt/"]')];
  const groups = [['过期', 'Overdue'], ['今天截止', 'Due Today'], ['本周截止', 'Due This Week'], ['以后截止', 'Due Later']];
  const items = [];
  let cursor = 0;
  for (let i = 0; i < links.length; i++) {
    const title = links[i].innerText.trim();
    const at = schedule.indexOf(title, cursor);
    if (!title || at < 0) return fail();
    const nextTitle = links[i + 1]?.innerText.trim();
    const nextAt = nextTitle ? schedule.indexOf(nextTitle, at + title.length) : schedule.length;
    const tail = schedule.slice(at + title.length, nextAt < 0 ? schedule.length : nextAt);
    const before = schedule.slice(0, at);
    let group = null, last = -1;
    for (const names of groups) for (const name of names) {
      const index = before.toLowerCase().lastIndexOf(name.toLowerCase());
      if (index > last) { last = index; group = names[0]; }
    }
    const due = tail.match(/逾期\s*\d+\s*天|截止\s*\d+月\s*\d+|今天截止|Due[^\n]+|\d+ days? overdue/i);
    if (!due || !group) return fail();
    const course = tail.slice(due.index + due[0].length).split(/\n/).map(s => s.trim()).filter(s => s && !groups.flat().includes(s))[0];
    const url = new URL(links[i].getAttribute('href'), 'https://bb.sustech.edu.cn');
    const id = url.pathname.split('/').pop();
    if (!course || url.origin !== 'https://bb.sustech.edu.cn' || !/^_blackboard\.platform\.gradebook2\.GradableItem-_[0-9]+_1$/.test(id)) return fail();
    items.push({id, title, course, due: due[0].replace(/\s+/g, ' ').trim(), group});
    cursor = at + title.length;
  }
  const explicitEmpty = /没有.*(作业|事件|截止)|无.*(作业|事件)|No (items|events|due dates)/i.test(schedule);
  return JSON.stringify({ready: new Set(items.map(x => x.id)).size === items.length && (items.length > 0 || explicitEmpty), items});
})();
