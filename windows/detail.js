(() => {
  let text = document.body?.innerText || '';
  const label = /到期日期|截止日期|Due Date/i;
  if (!label.test(text) || !/\d{1,2}:\d{2}/.test(text.slice(text.search(label), text.search(label) + 200))) {
    const button = [...document.querySelectorAll('button,a')].find(b => /作业详细信息|Assignment Details/i.test(b.innerText));
    if (button) button.click();
    text = document.body?.innerText || '';
  }
  const at = text.search(label);
  const part = at < 0 ? '' : text.slice(at, at + 200);
  const cn = part.match(/(\d{4})年\s*(\d{1,2})月\s*(\d{1,2})日[\s\S]{0,30}?(上午|下午|AM|PM)?\s*(\d{1,2}):(\d{2})/i);
  let dueAt = null;
  if (cn) {
    let h = Number(cn[5]);
    const period = cn[4]?.toUpperCase();
    if (period === '下午' || period === 'PM') h = h % 12 + 12;
    else if (period === '上午' || period === 'AM') h %= 12;
    const pad = n => String(n).padStart(2, '0');
    const candidate = `${cn[1]}-${pad(cn[2])}-${pad(cn[3])}T${pad(h)}:${cn[6]}:00+08:00`;
    if (h < 24 && Number(cn[6]) < 60 && !isNaN(Date.parse(candidate))) dueAt = candidate;
  }
  const submitted = /复查提交历史记录|Review Submission History/i.test(text) && /尝试|Attempt/i.test(text) ? true : null;
  return JSON.stringify({dueAt, submitted});
})();
