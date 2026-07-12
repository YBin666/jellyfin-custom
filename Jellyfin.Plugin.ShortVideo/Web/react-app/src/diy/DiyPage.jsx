import { ChevronLeft } from 'lucide-react';
import './diy.css';

export default function DiyPage({ onBack }) {
  return (
    <div className="diy-container">
      <div className="diy-header">
        <button className="diy-back" onClick={onBack}>
          <ChevronLeft />
        </button>
        <div className="diy-title">DIY</div>
        <div className="diy-spacer"></div>
      </div>
      <div className="diy-content">
        这是一个空白的 DIY 页面，待填充内容。
      </div>
    </div>
  );
}
